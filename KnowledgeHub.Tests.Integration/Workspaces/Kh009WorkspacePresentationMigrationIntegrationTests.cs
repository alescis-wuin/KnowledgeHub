using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Domain.ValueObjects;
using KnowledgeHub.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace KnowledgeHub.Tests.Integration.Workspaces;

/// <summary>
/// Runs real forward/backward migrations on disposable PostgreSQL 17 containers.
/// It must never connect to the developer database.
/// </summary>
public sealed class Kh009WorkspacePresentationMigrationIntegrationTests
{
    private const string BeforeWorkspaceIdentity = "20261031130000_AddExternalDocumentIdentity";
    private const string WithWorkspaceIdentity = "20261102140918_Kh009AddWorkspaceIdentity";

    private static readonly string[] NewTables =
    [
        "WorkspaceAssets",
        "WorkspaceAuditEvents",
        "WorkspaceBrandings",
        "WorkspaceHomeSections",
        "WorkspaceHomepages"
    ];

    [Theory]
    [InlineData(BeforeWorkspaceIdentity)]
    [InlineData(WithWorkspaceIdentity)]
    public async Task Additive_upgrade_and_downgrade_preserve_existing_rows(string startingMigration)
    {
        await using var container = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("kh009_presentation_upgrade_tests")
            .WithUsername("kh009_upgrade_tester")
            .WithPassword("kh009_test_only_password")
            .Build();
        await container.StartAsync();

        var options = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseNpgsql(container.GetConnectionString())
            .Options;
        await using var db = new KnowledgeDbContext(options);

        // First create the exact historical schema in this disposable database.
        await db.Database.MigrateAsync(startingMigration);
        var originalDocument = new KnowledgeDocument(
            "Historical KH009 document", "Historical content must not change across upgrades.");
        db.Documents.Add(originalDocument);

        Guid? userId = null;
        Guid? workspaceId = null;
        if (string.Equals(startingMigration, WithWorkspaceIdentity, StringComparison.Ordinal))
        {
            var user = new AppUser(new OidcIdentityKey(
                "https://id.kh009.example/upgrade",
                "user-" + Guid.NewGuid().ToString("N")), "Historical owner");
            var workspace = new Workspace(user.Id, "Historical workspace",
                "upgrade-" + Guid.NewGuid().ToString("N")[..12]);
            db.AppUsers.Add(user);
            db.Workspaces.Add(workspace);
            userId = user.Id;
            workspaceId = workspace.Id;
        }
        await db.SaveChangesAsync();
        var oldTables = await GetPublicTablesAsync(container.GetConnectionString());
        Assert.DoesNotContain(NewTables, table => oldTables.Contains(table));

        // Upgrade must add only the five presentation/audit tables.
        await db.Database.MigrateAsync();
        var afterUpgrade = await GetPublicTablesAsync(container.GetConnectionString());
        var added = afterUpgrade.Except(oldTables, StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal).ToArray();
        var expectedAdded = string.Equals(startingMigration, WithWorkspaceIdentity, StringComparison.Ordinal)
            ? NewTables
            : NewTables.Concat(new[]
            {
                "AppUsers", "Workspaces", "WorkspaceInvitations", "WorkspaceMembers"
            }).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        Assert.Equal(expectedAdded, added);
        Assert.True(oldTables.IsSubsetOf(afterUpgrade));
        await AssertLegacyRowsRemainAsync(db, originalDocument.Id, userId, workspaceId);
        Assert.False(db.Database.HasPendingModelChanges());
        var applied = await db.Database.GetAppliedMigrationsAsync();
        Assert.Contains(applied, migration => migration.EndsWith(
            "_Kh009AddWorkspacePresentationAndAudit", StringComparison.Ordinal));

        // Reversibility is qualified only on the same disposable container,
        // without any new-table data to preserve across downgrade.
        await db.Database.MigrateAsync(startingMigration);
        var afterDowngrade = await GetPublicTablesAsync(container.GetConnectionString());
        Assert.True(oldTables.SetEquals(afterDowngrade));
        await AssertLegacyRowsRemainAsync(db, originalDocument.Id, userId, workspaceId);

        await db.Database.MigrateAsync();
        var afterSecondUpgrade = await GetPublicTablesAsync(container.GetConnectionString());
        Assert.True(afterUpgrade.SetEquals(afterSecondUpgrade));
        await AssertLegacyRowsRemainAsync(db, originalDocument.Id, userId, workspaceId);
    }

    private static async Task AssertLegacyRowsRemainAsync(
        KnowledgeDbContext db, Guid documentId, Guid? userId, Guid? workspaceId)
    {
        var document = await db.Documents.AsNoTracking()
            .SingleAsync(item => item.Id == documentId);
        Assert.Equal("Historical KH009 document", document.Title);
        Assert.Equal("Historical content must not change across upgrades.", document.Content);
        if (userId.HasValue && workspaceId.HasValue)
        {
            Assert.NotNull(await db.AppUsers.AsNoTracking()
                .SingleOrDefaultAsync(user => user.Id == userId.Value));
            Assert.NotNull(await db.Workspaces.AsNoTracking()
                .SingleOrDefaultAsync(workspace => workspace.Id == workspaceId.Value));
        }
    }

    private static async Task<HashSet<string>> GetPublicTablesAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT table_name FROM information_schema.tables " +
            "WHERE table_schema = 'public' AND table_type = 'BASE TABLE'", connection);
        await using var reader = await command.ExecuteReaderAsync();
        var names = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }
        return names;
    }
}
