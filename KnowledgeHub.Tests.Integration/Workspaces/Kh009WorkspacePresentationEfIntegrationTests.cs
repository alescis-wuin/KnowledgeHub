using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Domain.ValueObjects;
using KnowledgeHub.Infrastructure.Persistence.Workspaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace KnowledgeHub.Tests.Integration.Workspaces;

/// <summary>
/// All mutations in this test class target a disposable PostgreSQL 17
/// Testcontainers database. The developer database is never contacted.
/// </summary>
public sealed class Kh009WorkspacePresentationEfFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("kh009_presentation_schema_tests")
        .WithUsername("kh009_presentation_tester")
        .WithPassword("kh009_test_only_password")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateContext();
        if (!await db.Database.EnsureCreatedAsync())
            throw new InvalidOperationException("The isolated schema was not created.");
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

public sealed class Kh009WorkspacePresentationEfIntegrationTests :
    IClassFixture<Kh009WorkspacePresentationEfFixture>
{
    private readonly Kh009WorkspacePresentationEfFixture _fixture;
    private static readonly DateTime Now = new(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc);

    public Kh009WorkspacePresentationEfIntegrationTests(
        Kh009WorkspacePresentationEfFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Branding_logo_fk_blocks_an_asset_owned_by_another_workspace()
    {
        Guid brandingWorkspaceId;
        Guid foreignAssetId;
        await using (var seed = _fixture.CreateContext())
        {
            var (user, a) = AddUserWorkspace(seed);
            var (_, b) = AddUserWorkspace(seed);
            var foreignAsset = NewAsset(b.Id, user.Id, WorkspaceAssetPurpose.LOGO);
            seed.WorkspaceAssets.Add(foreignAsset);
            seed.WorkspaceBrandings.Add(new WorkspaceBranding(
                a.Id, "Tenant A", WorkspaceBrandingColors.Default()));
            await seed.SaveChangesAsync();
            brandingWorkspaceId = a.Id;
            foreignAssetId = foreignAsset.Id;
        }

        await using var db = _fixture.CreateContext();
        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"WorkspaceBrandings\" SET \"LogoAssetId\" = {foreignAssetId} WHERE \"WorkspaceId\" = {brandingWorkspaceId}"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
    }

    [Fact]
    public async Task Asset_cannot_be_deleted_while_branding_references_it()
    {
        Guid id;
        await using (var seed = _fixture.CreateContext())
        {
            var (user, workspace) = AddUserWorkspace(seed);
            var logo = NewAsset(workspace.Id, user.Id, WorkspaceAssetPurpose.LOGO);
            seed.WorkspaceAssets.Add(logo);
            seed.WorkspaceBrandings.Add(new WorkspaceBranding(
                workspace.Id, "Brand", WorkspaceBrandingColors.Default(), logo));
            await seed.SaveChangesAsync();
            id = logo.Id;
        }

        await using var db = _fixture.CreateContext();
        var asset = await db.WorkspaceAssets.SingleAsync(x => x.Id == id);
        db.WorkspaceAssets.Remove(asset);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation,
            Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public async Task Homepage_links_json_round_trips_and_rejects_invalid_shape()
    {
        Guid sectionId;
        await using (var seed = _fixture.CreateContext())
        {
            var (_, workspace) = AddUserWorkspace(seed);
            var links = new[]
            {
                new WorkspaceHomeLink("Documents", WorkspaceHomeLinkTarget.DOCUMENTS),
                new WorkspaceHomeLink("More documents", WorkspaceHomeLinkTarget.DOCUMENTS),
                new WorkspaceHomeLink("Chat", WorkspaceHomeLinkTarget.CHAT)
            };
            var section = new WorkspaceHomeSection(
                workspace.Id, WorkspaceSectionType.LINKS, "Quick links", null, 0, links);
            seed.WorkspaceHomepages.Add(new WorkspaceHomepage(
                workspace.Id, "Home", sections: new[] { section }));
            await seed.SaveChangesAsync();
            sectionId = section.Id;
        }

        await using var db = _fixture.CreateContext();
        var page = await db.WorkspaceHomepages
            .Include(homepage => homepage.Sections)
            .SingleAsync(homepage => homepage.Sections.Any(section => section.Id == sectionId));
        var loaded = Assert.Single(page.Sections);
        Assert.Equal(3, loaded.Links.Count);
        Assert.Equal("More documents", loaded.Links[1].Label);
        Assert.Equal(WorkspaceHomeLinkTarget.DOCUMENTS, loaded.Links[1].Target);
        var invalidJson = "{\"unexpected\": true}";
        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"WorkspaceHomeSections\" SET \"LinksJson\" = {invalidJson}::jsonb WHERE \"Id\" = {sectionId}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
    }

    [Fact]
    public async Task Section_order_is_tenant_unique_and_never_exceeds_nineteen()
    {
        Guid workspaceId;
        await using (var seed = _fixture.CreateContext())
        {
            var (_, workspace) = AddUserWorkspace(seed);
            seed.WorkspaceHomepages.Add(new WorkspaceHomepage(workspace.Id, "Home",
                sections: new[]
                {
                    new WorkspaceHomeSection(workspace.Id, WorkspaceSectionType.STATS, "Stats", null, 0)
                }));
            await seed.SaveChangesAsync();
            workspaceId = workspace.Id;
        }

        await using (var db = _fixture.CreateContext())
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(() =>
                db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE \"WorkspaceHomeSections\" SET \"SortOrder\" = 20 WHERE \"WorkspaceId\" = {workspaceId}"));
            Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        }
        await using (var db = _fixture.CreateContext())
        {
            db.WorkspaceHomeSections.Add(new WorkspaceHomeSection(
                workspaceId, WorkspaceSectionType.STATS, "Duplicate order", null, 0));
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation,
                Assert.IsType<PostgresException>(ex.InnerException).SqlState);
        }
    }

    [Fact]
    public async Task Asset_size_mime_and_dimensions_are_checked_inside_postgresql()
    {
        Guid assetId;
        await using (var seed = _fixture.CreateContext())
        {
            var (user, workspace) = AddUserWorkspace(seed);
            var asset = NewAsset(workspace.Id, user.Id, WorkspaceAssetPurpose.LOGO);
            seed.WorkspaceAssets.Add(asset);
            await seed.SaveChangesAsync();
            assetId = asset.Id;
        }

        await using var db = _fixture.CreateContext();
        // Keep the four SQL commands fixed: interpolated asset IDs are
        // parameterized and EF1003 cannot be triggered by SQL concatenation.
        Func<Task<int>>[] invalidUpdates =
        [
            () => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"WorkspaceAssets\" SET \"Length\" = 5242881 WHERE \"Id\" = {assetId}"),
            () => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"WorkspaceAssets\" SET \"ContentType\" = 'image/svg+xml' WHERE \"Id\" = {assetId}"),
            () => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"WorkspaceAssets\" SET \"PixelWidth\" = 8193 WHERE \"Id\" = {assetId}"),
            () => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"WorkspaceAssets\" SET \"Sha256\" = 'not-a-hash' WHERE \"Id\" = {assetId}")
        ];

        foreach (var update in invalidUpdates)
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(async () =>
            {
                await update();
            });
            Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        }
    }

    [Fact]
    public async Task Branding_and_homepage_versions_prevent_lost_updates()
    {
        Guid workspaceId;
        await using (var seed = _fixture.CreateContext())
        {
            var (_, workspace) = AddUserWorkspace(seed);
            seed.WorkspaceBrandings.Add(new WorkspaceBranding(
                workspace.Id, "Original", WorkspaceBrandingColors.Default()));
            seed.WorkspaceHomepages.Add(new WorkspaceHomepage(workspace.Id, "Original"));
            await seed.SaveChangesAsync();
            workspaceId = workspace.Id;
        }

        await using var first = _fixture.CreateContext();
        await using var second = _fixture.CreateContext();
        var a = await first.WorkspaceBrandings.SingleAsync(x => x.WorkspaceId == workspaceId);
        var b = await second.WorkspaceBrandings.SingleAsync(x => x.WorkspaceId == workspaceId);
        a.Replace("First", WorkspaceBrandingColors.Default(), null, null, 1);
        b.Replace("Stale", WorkspaceBrandingColors.Default(), null, null, 1);
        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

        await using var third = _fixture.CreateContext();
        await using var fourth = _fixture.CreateContext();
        var h1 = await third.WorkspaceHomepages.SingleAsync(x => x.WorkspaceId == workspaceId);
        var h2 = await fourth.WorkspaceHomepages.SingleAsync(x => x.WorkspaceId == workspaceId);
        h1.Replace("First", null, null, Array.Empty<WorkspaceHomeSection>(), 1);
        h2.Replace("Stale", null, null, Array.Empty<WorkspaceHomeSection>(), 1);
        await third.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => fourth.SaveChangesAsync());
    }


    [Fact]
    public async Task Section_requires_an_existing_homepage_of_the_same_workspace()
    {
        Guid unrelatedWorkspaceId;
        await using (var seed = _fixture.CreateContext())
        {
            var (_, workspace) = AddUserWorkspace(seed);
            await seed.SaveChangesAsync();
            unrelatedWorkspaceId = workspace.Id;
        }

        await using var db = _fixture.CreateContext();
        db.WorkspaceHomeSections.Add(new WorkspaceHomeSection(
            unrelatedWorkspaceId, WorkspaceSectionType.STATS, "Orphan", null, 0));
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation,
            Assert.IsType<PostgresException>(ex.InnerException).SqlState);
    }

    [Fact]
    public async Task Audit_event_and_target_mismatch_is_rejected_by_sql()
    {
        Guid id;
        await using (var seed = _fixture.CreateContext())
        {
            var (_, workspace) = AddUserWorkspace(seed);
            var audit = WorkspaceAuditEvent.ForSystem(workspace.Id,
                WorkspaceAuditEventType.WORKSPACE_CREATED,
                WorkspaceAuditTargetType.WORKSPACE,
                workspace.Id,
                WorkspaceAuditOutcome.SUCCEEDED,
                Now);
            seed.WorkspaceAuditEvents.Add(audit);
            await seed.SaveChangesAsync();
            id = audit.Id;
        }

        await using var db = _fixture.CreateContext();
        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"WorkspaceAuditEvents\" SET \"TargetType\" = 5 WHERE \"Id\" = {id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
    }

    [Fact]
    public async Task Audit_is_append_only_for_tracked_writes_and_enums_are_sql_checked()
    {
        Guid auditId;
        await using (var seed = _fixture.CreateContext())
        {
            var (_, workspace) = AddUserWorkspace(seed);
            var audit = WorkspaceAuditEvent.ForSystem(workspace.Id,
                WorkspaceAuditEventType.WORKSPACE_CREATED,
                WorkspaceAuditTargetType.WORKSPACE,
                workspace.Id,
                WorkspaceAuditOutcome.SUCCEEDED,
                Now);
            seed.WorkspaceAuditEvents.Add(audit);
            await seed.SaveChangesAsync();
            auditId = audit.Id;
        }

        await using var db = _fixture.CreateContext();
        var loaded = await db.WorkspaceAuditEvents.SingleAsync(x => x.Id == auditId);
        db.Entry(loaded).Property(x => x.Outcome).CurrentValue = WorkspaceAuditOutcome.FAILED;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.Entry(loaded).State = EntityState.Unchanged;
        db.WorkspaceAuditEvents.Remove(loaded);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());

        await using var direct = _fixture.CreateContext();
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            direct.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"WorkspaceAuditEvents\" SET \"Outcome\" = 99 WHERE \"Id\" = {auditId}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }

    private static (AppUser User, Workspace Workspace) AddUserWorkspace(
        WorkspaceSchemaValidationContext db)
    {
        var user = new AppUser(new OidcIdentityKey(
            "https://id.kh009.example", "sub-" + Guid.NewGuid().ToString("N")), "Test user");
        var workspace = new Workspace(user.Id, "Staged presentation",
            "stage-" + Guid.NewGuid().ToString("N")[..12], null, Now);
        db.AppUsers.Add(user);
        db.Workspaces.Add(workspace);
        return (user, workspace);
    }

    private static WorkspaceAsset NewAsset(
        Guid workspaceId, Guid userId, WorkspaceAssetPurpose purpose) => new(
        workspaceId, userId, purpose, "image/png",
        Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
        new string('a', 64), 128, 16, 16, Now);
}
