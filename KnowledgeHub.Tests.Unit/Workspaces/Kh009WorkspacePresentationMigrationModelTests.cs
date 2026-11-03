using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace KnowledgeHub.Tests.Unit.Workspaces;

/// <summary>
/// Verifies the scaffolded migration contains only additive schema operations.
/// No SQL connection is opened by these tests.
/// </summary>
public sealed class Kh009WorkspacePresentationMigrationModelTests
{
    private static readonly string[] ExpectedTables =
    [
        "WorkspaceAssets",
        "WorkspaceAuditEvents",
        "WorkspaceBrandings",
        "WorkspaceHomeSections",
        "WorkspaceHomepages"
    ];

    [Fact]
    public void Production_model_includes_all_five_presentation_and_audit_entities()
    {
        using var db = CreateContext();
        foreach (var entity in new[]
        {
            typeof(WorkspaceBranding),
            typeof(WorkspaceHomepage),
            typeof(WorkspaceHomeSection),
            typeof(WorkspaceAsset),
            typeof(WorkspaceAuditEvent)
        })
        {
            Assert.NotNull(db.Model.FindEntityType(entity));
        }
    }

    [Fact]
    public void Migration_up_only_creates_the_five_tables_and_their_indexes_and_constraints()
    {
        using var db = CreateContext();
        var migration = GetPresentationMigration(db);
        var tables = migration.UpOperations.OfType<CreateTableOperation>()
            .Select(operation => operation.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedTables, tables);
        Assert.All(migration.UpOperations, operation => Assert.Contains(
            operation.GetType().Name,
            new[]
            {
                nameof(CreateTableOperation),
                nameof(CreateIndexOperation),
                nameof(AddForeignKeyOperation),
                nameof(AddCheckConstraintOperation),
                nameof(AddUniqueConstraintOperation),
                nameof(AddPrimaryKeyOperation)
            }));

        Assert.All(migration.DownOperations, operation => Assert.Contains(
            operation.GetType().Name,
            new[]
            {
                nameof(DropTableOperation),
                nameof(DropIndexOperation),
                nameof(DropForeignKeyOperation),
                nameof(DropCheckConstraintOperation)
            }));
        Assert.Equal(5, migration.DownOperations.OfType<DropTableOperation>().Count());
    }

    private static Migration GetPresentationMigration(KnowledgeDbContext db)
    {
        var migrationsAssembly = db.GetService<IMigrationsAssembly>();
        var found = Assert.Single(migrationsAssembly.Migrations,
            pair => pair.Key.EndsWith(
                "_Kh009AddWorkspacePresentationAndAudit", StringComparison.Ordinal));
        return migrationsAssembly.CreateMigration(found.Value, db.Database.ProviderName!);
    }

    private static KnowledgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseNpgsql("Host=localhost;Database=not_connected;Username=test;Password=test")
            .Options;
        return new KnowledgeDbContext(options);
    }
}
