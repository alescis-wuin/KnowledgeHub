using KnowledgeHub.Application.Authorization;
using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Domain.ValueObjects;
using KnowledgeHub.Infrastructure.Persistence.Context;
using KnowledgeHub.Infrastructure.Persistence.Workspaces;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace KnowledgeHub.Tests.Integration.Workspaces;

/// <summary>
/// Dedicated disposable PostgreSQL 17 container. Upgrades a populated legacy
/// schema and then verifies the actual additive migration and EF access store.
/// Never connects to the developer database.
/// </summary>
public sealed class Kh009WorkspaceMigrationFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("kh009_workspace_migration_tests")
            .WithUsername("kh009_migration_tester")
            .WithPassword("kh009_test_only_password")
            .Build();

    public Guid PreservedDocumentId { get; private set; }
    public string PreservedContentHash { get; private set; } = string.Empty;
    public IReadOnlyDictionary<string, int> WorkspaceTableCountsAfterMigration { get; private set; }
        = new Dictionary<string, int>();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var context = CreateContext();
        // Reproduce the previous schema and leave a real historical row in it.
        await context.Database.MigrateAsync("20261031130000_AddExternalDocumentIdentity");
        var document = new KnowledgeDocument(
            "Historical document - KH009 migration",
            "This legacy document must survive the workspace migration unchanged.");
        context.Documents.Add(document);
        await context.SaveChangesAsync();
        PreservedDocumentId = document.Id;
        PreservedContentHash = document.ContentSha256!;

        // Upgrade only this isolated container to the latest migration.
        await context.Database.MigrateAsync();

        // Capture every count immediately after the upgrade, before any test
        // writes to the shared class fixture. No test execution order is assumed.
        WorkspaceTableCountsAfterMigration =
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(
                new Dictionary<string, int>
                {
                    ["AppUsers"] = await context.AppUsers.CountAsync(),
                    ["Workspaces"] = await context.Workspaces.CountAsync(),
                    ["WorkspaceMembers"] = await context.WorkspaceMembers.CountAsync(),
                    ["WorkspaceInvitations"] = await context.WorkspaceInvitations.CountAsync()
                });
    }

    public KnowledgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;
        return new KnowledgeDbContext(options);
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

public sealed class Kh009WorkspaceMigrationIntegrationTests
    : IClassFixture<Kh009WorkspaceMigrationFixture>
{
    private readonly Kh009WorkspaceMigrationFixture _fixture;

    public Kh009WorkspaceMigrationIntegrationTests(
        Kh009WorkspaceMigrationFixture fixture) => _fixture = fixture;

    [Fact]
    public Task Upgrade_preserves_historical_documents_and_creates_four_tables() =>
        VerifyHistoricalUpgradeAsync();

    private async Task VerifyHistoricalUpgradeAsync()
    {
        await using var db = _fixture.CreateContext();
        var historical = await db.Documents.AsNoTracking()
            .SingleAsync(d => d.Id == _fixture.PreservedDocumentId);
        Assert.Equal("Historical document - KH009 migration", historical.Title);
        Assert.Equal(_fixture.PreservedContentHash, historical.ContentSha256);
        Assert.Equal("This legacy document must survive the workspace migration unchanged.",
            historical.Content);
        Assert.Equal(4, _fixture.WorkspaceTableCountsAfterMigration.Count);
        Assert.All(_fixture.WorkspaceTableCountsAfterMigration,
            count => Assert.Equal(0, count.Value));
        var applied = await db.Database.GetAppliedMigrationsAsync();
        Assert.Contains(applied, migration =>
            migration.EndsWith("_Kh009AddWorkspaceIdentity", StringComparison.Ordinal));
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Historical_upgrade_assertions_survive_later_workspace_writes()
    {
        var identity = new OidcIdentityKey(
            "https://id.kh009.example/regression", Guid.NewGuid().ToString("N"));
        var user = new AppUser(identity, "Fixture isolation regression");
        await using (var db = _fixture.CreateContext())
        {
            db.AppUsers.Add(user);
            db.Workspaces.Add(NewWorkspace(user.Id));
            await db.SaveChangesAsync();
            Assert.True(await db.AppUsers.CountAsync() > 0);
            Assert.True(await db.Workspaces.CountAsync() > 0);
        }

        // Exercise the same preservation assertions after intentional writes.
        // This fails with the old current-table-count assertions, regardless
        // of which order xUnit chooses for the other tests in this class.
        await VerifyHistoricalUpgradeAsync();
    }

    [Fact]
    public async Task Trusted_oidc_pair_and_membership_are_required_for_lookup()
    {
        var issuer = "https://id.kh009.example/" + Guid.NewGuid().ToString("N");
        var identity = new OidcIdentityKey(issuer, "Subject-ABC");
        var user = new AppUser(identity, "Workspace owner");
        var outsider = new AppUser(new OidcIdentityKey(issuer, "outsider-" + Guid.NewGuid().ToString("N")), "Outsider");
        var workspace = NewWorkspace(user.Id);
        var otherWorkspace = NewWorkspace(outsider.Id);

        await using var db = _fixture.CreateContext();
        db.AppUsers.AddRange(user, outsider);
        db.Workspaces.AddRange(workspace, otherWorkspace);
        db.WorkspaceMembers.Add(new WorkspaceMember(workspace.Id, user.Id, WorkspaceRole.OWNER));
        db.WorkspaceMembers.Add(new WorkspaceMember(otherWorkspace.Id, outsider.Id, WorkspaceRole.OWNER));
        await db.SaveChangesAsync();

        var store = new EfWorkspaceAccessStore(db);
        var match = await store.FindAsync(identity, workspace.Id);
        Assert.NotNull(match);
        Assert.Equal(WorkspaceRole.OWNER, match.Role);
        Assert.Equal(WorkspaceMemberStatus.ACTIVE, match.Status);
        Assert.False(match.IsArchived);
        Assert.Null(await store.FindAsync(identity, otherWorkspace.Id));
        Assert.Null(await store.FindAsync(
            new OidcIdentityKey(issuer, "subject-abc"), workspace.Id));
        Assert.Null(await store.FindAsync(
            new OidcIdentityKey(issuer + "-wrong", identity.Subject), workspace.Id));
        Assert.Null(await store.FindAsync(identity, Guid.Empty));
    }

    [Fact]
    public async Task Suspended_membership_and_archived_workspace_are_exposed_for_fail_closed_handler()
    {
        var issuer = "https://id.kh009.example/" + Guid.NewGuid().ToString("N");
        var identity = new OidcIdentityKey(issuer, "subject-" + Guid.NewGuid().ToString("N"));
        var user = new AppUser(identity, "Workspace viewer");
        var workspace = NewWorkspace(user.Id);
        var member = new WorkspaceMember(workspace.Id, user.Id, WorkspaceRole.VIEWER);
        await using var db = _fixture.CreateContext();
        db.AppUsers.Add(user);
        db.Workspaces.Add(workspace);
        db.WorkspaceMembers.Add(member);
        await db.SaveChangesAsync();

        var store = new EfWorkspaceAccessStore(db);
        var active = await store.FindAsync(identity, workspace.Id);
        Assert.NotNull(active);
        Assert.Equal(WorkspaceMemberStatus.ACTIVE, active.Status);

        member.Suspend();
        workspace.Archive(workspace.Version, DateTime.UtcNow);
        await db.SaveChangesAsync();
        var blocked = await store.FindAsync(identity, workspace.Id);
        Assert.NotNull(blocked);
        Assert.Equal(WorkspaceMemberStatus.SUSPENDED, blocked.Status);
        Assert.True(blocked.IsArchived);
    }

    [Fact]
    public async Task Migration_tables_preserve_oidc_and_slug_uniqueness_constraints()
    {
        var issuer = "https://id.kh009.example/" + Guid.NewGuid().ToString("N");
        var identity = new OidcIdentityKey(issuer, "Subject-" + Guid.NewGuid().ToString("N"));
        var user = new AppUser(identity, "Unique user");
        var workspace = NewWorkspace(user.Id);
        await using (var db = _fixture.CreateContext())
        {
            db.AppUsers.Add(user);
            db.Workspaces.Add(workspace);
            await db.SaveChangesAsync();
        }
        await using (var db = _fixture.CreateContext())
        {
            db.AppUsers.Add(new AppUser(identity, "Duplicate user"));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        await using (var db = _fixture.CreateContext())
        {
            db.Workspaces.Add(new Workspace(user.Id, "Duplicate slug", workspace.SlugNormalized));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }

    private static Workspace NewWorkspace(Guid creatorId) =>
        new(creatorId, "Migration verification", "kh-" + Guid.NewGuid().ToString("N")[..12]);
}
