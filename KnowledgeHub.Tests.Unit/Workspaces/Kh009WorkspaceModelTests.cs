using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Infrastructure.Persistence.Context;
using KnowledgeHub.Infrastructure.Persistence.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeHub.Tests.Unit.Workspaces;

public sealed class Kh009WorkspaceModelTests
{
    [Fact]
    public void Production_model_excludes_staged_workspace_tables_until_migration()
    {
        var options = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseNpgsql("Host=localhost;Database=not_connected;Username=test;Password=test")
            .Options;
        using var db = new KnowledgeDbContext(options);
        Assert.Null(db.Model.FindEntityType(typeof(Workspace)));
        Assert.Null(db.Model.FindEntityType(typeof(WorkspaceMember)));
        Assert.Null(db.Model.FindEntityType(typeof(WorkspaceInvitation)));
        Assert.Null(db.Model.FindEntityType(typeof(AppUser)));
        Assert.NotNull(db.Model.FindEntityType(typeof(KnowledgeDocument)));
    }

    [Fact]
    public void Staged_model_has_expected_composite_key_unique_indexes_and_concurrency()
    {
        var options = new DbContextOptionsBuilder<WorkspaceSchemaValidationContext>()
            .UseNpgsql("Host=localhost;Database=not_connected;Username=test;Password=test")
            .Options;
        using var db = new WorkspaceSchemaValidationContext(options);

        var appUser = Assert.IsAssignableFrom<Microsoft.EntityFrameworkCore.Metadata.IEntityType>(
            db.Model.FindEntityType(typeof(AppUser)));
        Assert.Contains(appUser.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(new[] { "Issuer", "Subject" }));
        Assert.DoesNotContain(appUser.GetIndexes(), index => index.IsUnique &&
            index.Properties.Any(p => p.Name == "EmailNormalized"));

        var workspace = Assert.IsAssignableFrom<Microsoft.EntityFrameworkCore.Metadata.IEntityType>(
            db.Model.FindEntityType(typeof(Workspace)));
        Assert.Contains(workspace.GetIndexes(), index => index.IsUnique &&
            index.Properties.SingleOrDefault()?.Name == "SlugNormalized");
        Assert.True(workspace.FindProperty(nameof(Workspace.Version))!.IsConcurrencyToken);

        var member = Assert.IsAssignableFrom<Microsoft.EntityFrameworkCore.Metadata.IEntityType>(
            db.Model.FindEntityType(typeof(WorkspaceMember)));
        Assert.Equal(new[] { "WorkspaceId", "UserId" },
            member.FindPrimaryKey()!.Properties.Select(p => p.Name));
        Assert.True(member.FindProperty(nameof(WorkspaceMember.Version))!.IsConcurrencyToken);

        var invitation = Assert.IsAssignableFrom<Microsoft.EntityFrameworkCore.Metadata.IEntityType>(
            db.Model.FindEntityType(typeof(WorkspaceInvitation)));
        Assert.Contains(invitation.GetIndexes(), index => index.IsUnique &&
            index.Properties.SingleOrDefault()?.Name == "TokenHash");
        Assert.True(invitation.FindProperty(nameof(WorkspaceInvitation.Version))!.IsConcurrencyToken);
        Assert.Equal(3, invitation.GetForeignKeys().Count());
    }
}
