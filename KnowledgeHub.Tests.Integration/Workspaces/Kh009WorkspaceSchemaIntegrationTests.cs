using System.Security.Cryptography;
using System.Text;
using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Domain.ValueObjects;
using KnowledgeHub.Infrastructure.Persistence.Workspaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace KnowledgeHub.Tests.Integration.Workspaces;

/// <summary>
/// Creates a completely separate PostgreSQL container and schema. Never calls
/// MigrateAsync or EnsureCreatedAsync on the production KnowledgeDbContext.
/// </summary>
public sealed class Kh009WorkspaceSchemaFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("kh009_workspace_schema_tests")
            .WithUsername("kh009_tester")
            .WithPassword("kh009_test_only_password")
            .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateContext();
        if (!await db.Database.EnsureCreatedAsync())
            throw new InvalidOperationException("Fresh test database was not created.");
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public WorkspaceSchemaValidationContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WorkspaceSchemaValidationContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;
        return new WorkspaceSchemaValidationContext(options);
    }
}

public sealed class Kh009WorkspaceSchemaIntegrationTests : IClassFixture<Kh009WorkspaceSchemaFixture>
{
    private readonly Kh009WorkspaceSchemaFixture _fixture;
    private static readonly DateTime Now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    public Kh009WorkspaceSchemaIntegrationTests(Kh009WorkspaceSchemaFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Same_oidc_pair_is_unique_but_subject_case_and_email_are_not_keys()
    {
        var issuer = "https://id.example.org/" + Guid.NewGuid().ToString("N");
        var subject = "Subject-" + Guid.NewGuid().ToString("N");
        await using (var db = _fixture.CreateContext())
        {
            var first = new AppUser(new OidcIdentityKey(issuer, subject), "First");
            first.SetProfile("First", "shared@example.org", true);
            db.AppUsers.Add(first);
            await db.SaveChangesAsync();
        }
        await using (var db = _fixture.CreateContext())
        {
            var duplicate = new AppUser(new OidcIdentityKey(issuer, subject), "Duplicate");
            db.AppUsers.Add(duplicate);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        await using (var db = _fixture.CreateContext())
        {
            var differentCase = new AppUser(new OidcIdentityKey(issuer, subject.ToLowerInvariant()), "Other");
            differentCase.SetProfile("Other", "shared@example.org", true);
            db.AppUsers.Add(differentCase);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Workspace_slug_is_unique_and_creator_foreign_key_is_required()
    {
        var slug = "slug-" + Guid.NewGuid().ToString("N")[..12];
        var creator = NewUser();
        await using (var db = _fixture.CreateContext())
        {
            db.AppUsers.Add(creator);
            db.Workspaces.Add(new Workspace(creator.Id, "First", slug, null, Now));
            await db.SaveChangesAsync();
        }
        await using (var db = _fixture.CreateContext())
        {
            db.Workspaces.Add(new Workspace(creator.Id, "Second", slug, null, Now));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        await using (var db = _fixture.CreateContext())
        {
            db.Workspaces.Add(new Workspace(Guid.NewGuid(), "Invalid creator", "broken-" + Guid.NewGuid().ToString("N")[..8], null, Now));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task Composite_membership_key_allows_same_user_in_two_workspaces()
    {
        var creator = NewUser();
        var viewer = NewUser();
        var a = NewWorkspace(creator.Id);
        var b = NewWorkspace(creator.Id);
        await using (var db = _fixture.CreateContext())
        {
            db.AppUsers.AddRange(creator, viewer);
            db.Workspaces.AddRange(a, b);
            db.WorkspaceMembers.AddRange(
                new WorkspaceMember(a.Id, viewer.Id, WorkspaceRole.VIEWER, Now),
                new WorkspaceMember(b.Id, viewer.Id, WorkspaceRole.EDITOR, Now));
            await db.SaveChangesAsync();
        }
        await using (var db = _fixture.CreateContext())
        {
            db.WorkspaceMembers.Add(new WorkspaceMember(a.Id, viewer.Id, WorkspaceRole.MEMBER, Now));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        await using (var db = _fixture.CreateContext())
        {
            db.WorkspaceMembers.Add(new WorkspaceMember(a.Id, Guid.NewGuid(), WorkspaceRole.VIEWER, Now));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task Invitation_token_hash_is_unique_and_expiration_and_role_checks_are_active()
    {
        var creator = NewUser();
        var workspace = NewWorkspace(creator.Id);
        var hash = Hash("token-" + Guid.NewGuid());
        WorkspaceInvitation invitation;
        await using (var db = _fixture.CreateContext())
        {
            db.AppUsers.Add(creator);
            db.Workspaces.Add(workspace);
            invitation = new WorkspaceInvitation(workspace.Id, creator.Id,
                "invite@example.org", WorkspaceRole.MEMBER, hash, Now.AddDays(7), Now);
            db.WorkspaceInvitations.Add(invitation);
            await db.SaveChangesAsync();
        }
        await using (var db = _fixture.CreateContext())
        {
            db.WorkspaceInvitations.Add(new WorkspaceInvitation(workspace.Id, creator.Id,
                "another@example.org", WorkspaceRole.EDITOR, hash, Now.AddDays(1), Now));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        await using (var db = _fixture.CreateContext())
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(async () =>
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE \"WorkspaceInvitations\" SET \"Role\" = 4 WHERE \"Id\" = {invitation.Id}");
            });
            Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        }
        await using (var db = _fixture.CreateContext())
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(async () =>
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE \"WorkspaceInvitations\" SET \"ExpiresAtUtc\" = {Now} WHERE \"Id\" = {invitation.Id}");
            });
            Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        }
    }

    [Fact]
    public async Task Suspended_owner_is_rejected_by_database_even_with_direct_sql()
    {
        var user = NewUser();
        var workspace = NewWorkspace(user.Id);
        await using (var db = _fixture.CreateContext())
        {
            db.AppUsers.Add(user);
            db.Workspaces.Add(workspace);
            db.WorkspaceMembers.Add(new WorkspaceMember(workspace.Id, user.Id, WorkspaceRole.OWNER, Now));
            await db.SaveChangesAsync();
        }
        await using (var db = _fixture.CreateContext())
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(async () =>
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE \"WorkspaceMembers\" SET \"Status\" = 2 WHERE \"WorkspaceId\" = {workspace.Id} AND \"UserId\" = {user.Id}");
            });
            Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        }
    }

    [Fact]
    public async Task Workspace_version_prevents_lost_updates_from_two_contexts()
    {
        var user = NewUser();
        var workspace = NewWorkspace(user.Id);
        await using (var seed = _fixture.CreateContext())
        {
            seed.AppUsers.Add(user);
            seed.Workspaces.Add(workspace);
            await seed.SaveChangesAsync();
        }
        await using var first = _fixture.CreateContext();
        await using var second = _fixture.CreateContext();
        var a = await first.Workspaces.SingleAsync(w => w.Id == workspace.Id);
        var b = await second.Workspaces.SingleAsync(w => w.Id == workspace.Id);
        a.UpdateInformation("First update", null, 1);
        b.UpdateInformation("Stale update", null, 1);
        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    private static AppUser NewUser() =>
        new(new OidcIdentityKey("https://id.example.org", "sub-" + Guid.NewGuid().ToString("N")), "Test User");

    private static Workspace NewWorkspace(Guid creatorId) =>
        new(creatorId, "Workspace", "work-" + Guid.NewGuid().ToString("N")[..12], null, Now);

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
