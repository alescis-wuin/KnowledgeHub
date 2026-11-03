using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Infrastructure.Persistence.Context;
using KnowledgeHub.Infrastructure.Persistence.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeHub.Tests.Unit.Workspaces;

public sealed class Kh009WorkspacePresentationEfModelTests
{
    private static readonly Type[] StagedEntities =
    {
        typeof(WorkspaceBranding), typeof(WorkspaceHomepage),
        typeof(WorkspaceHomeSection), typeof(WorkspaceAsset),
        typeof(WorkspaceAuditEvent)
    };

    [Fact]
    public void Production_model_includes_five_presentation_entities_and_keeps_identity()
    {
        var options = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseNpgsql("Host=localhost;Database=not_connected;Username=test;Password=test")
            .Options;
        using var db = new KnowledgeDbContext(options);

        Assert.All(StagedEntities, type => Assert.NotNull(db.Model.FindEntityType(type)));
        Assert.NotNull(db.Model.FindEntityType(typeof(AppUser)));
        Assert.NotNull(db.Model.FindEntityType(typeof(Workspace)));
        Assert.NotNull(db.Model.FindEntityType(typeof(WorkspaceMember)));
        Assert.NotNull(db.Model.FindEntityType(typeof(WorkspaceInvitation)));
        Assert.NotNull(db.Model.FindEntityType(typeof(KnowledgeDocument)));
    }

    [Fact]
    public void Staging_model_registers_five_entities_tenant_keys_and_concurrency()
    {
        var options = new DbContextOptionsBuilder<WorkspaceSchemaValidationContext>()
            .UseNpgsql("Host=localhost;Database=not_connected;Username=test;Password=test")
            .Options;
        using var db = new WorkspaceSchemaValidationContext(options);

        Assert.All(StagedEntities, type => Assert.NotNull(db.Model.FindEntityType(type)));

        var branding = db.Model.FindEntityType(typeof(WorkspaceBranding))!;
        Assert.Equal(new[] { nameof(WorkspaceBranding.WorkspaceId) },
            branding.FindPrimaryKey()!.Properties.Select(p => p.Name));
        Assert.True(branding.FindProperty(nameof(WorkspaceBranding.Version))!.IsConcurrencyToken);
        foreach (var assetId in new[]
        {
            nameof(WorkspaceBranding.LogoAssetId),
            nameof(WorkspaceBranding.BannerAssetId)
        })
        {
            Assert.Contains(branding.GetForeignKeys(), fk =>
                fk.PrincipalEntityType.ClrType == typeof(WorkspaceAsset) &&
                fk.Properties.Select(p => p.Name).SequenceEqual(new[]
                {
                    nameof(WorkspaceBranding.WorkspaceId), assetId
                }));
        }

        var homepage = db.Model.FindEntityType(typeof(WorkspaceHomepage))!;
        Assert.True(homepage.FindProperty(nameof(WorkspaceHomepage.Version))!.IsConcurrencyToken);
        var section = db.Model.FindEntityType(typeof(WorkspaceHomeSection))!;
        Assert.Contains(section.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(new[]
            {
                nameof(WorkspaceHomeSection.WorkspaceId), nameof(WorkspaceHomeSection.SortOrder)
            }));
        Assert.NotNull(section.FindProperty(nameof(WorkspaceHomeSection.Links)));
        Assert.Contains(section.GetForeignKeys(), fk =>
            fk.PrincipalEntityType.ClrType == typeof(WorkspaceHomepage));

        var asset = db.Model.FindEntityType(typeof(WorkspaceAsset))!;
        Assert.Contains(asset.GetKeys(), key => key.Properties.Select(p => p.Name)
            .SequenceEqual(new[] { nameof(WorkspaceAsset.WorkspaceId), nameof(WorkspaceAsset.Id) }));
    }
}
