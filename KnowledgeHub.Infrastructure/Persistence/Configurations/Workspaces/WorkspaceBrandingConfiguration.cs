using KnowledgeHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KnowledgeHub.Infrastructure.Persistence.Configurations.Workspaces;

public sealed class WorkspaceBrandingConfiguration :
    IEntityTypeConfiguration<WorkspaceBranding>, IWorkspacePresentationStagedConfiguration
{
    public void Configure(EntityTypeBuilder<WorkspaceBranding> builder)
    {
        builder.ToTable("WorkspaceBrandings", table =>
        {
            table.HasCheckConstraint("CK_WorkspaceBrandings_VersionPositive", "\"Version\" >= 1");
            table.HasCheckConstraint("CK_WorkspaceBrandings_DisplayNameNotBlank",
                "length(btrim(\"DisplayName\")) > 0");
            table.HasCheckConstraint("CK_WorkspaceBrandings_DistinctAssets",
                "\"LogoAssetId\" IS NULL OR \"BannerAssetId\" IS NULL OR \"LogoAssetId\" <> \"BannerAssetId\"");
            foreach (var name in new[]
            {
                "PrimaryColor", "SecondaryColor", "AccentColor", "BackgroundColor",
                "SurfaceColor", "TextColor", "TextOnPrimaryColor"
            })
            {
                table.HasCheckConstraint("CK_WorkspaceBrandings_" + name,
                    "\"" + name + "\" ~ '^#[0-9A-F]{6}$'");
            }
        });

        builder.HasKey(branding => branding.WorkspaceId);
        builder.Property(branding => branding.DisplayName).HasMaxLength(150).IsRequired();
        builder.Property(branding => branding.Version).IsRequired().IsConcurrencyToken();
        builder.Property(branding => branding.PrimaryColor).HasMaxLength(7).UseCollation("C").IsRequired();
        builder.Property(branding => branding.SecondaryColor).HasMaxLength(7).UseCollation("C").IsRequired();
        builder.Property(branding => branding.AccentColor).HasMaxLength(7).UseCollation("C").IsRequired();
        builder.Property(branding => branding.BackgroundColor).HasMaxLength(7).UseCollation("C").IsRequired();
        builder.Property(branding => branding.SurfaceColor).HasMaxLength(7).UseCollation("C").IsRequired();
        builder.Property(branding => branding.TextColor).HasMaxLength(7).UseCollation("C").IsRequired();
        builder.Property(branding => branding.TextOnPrimaryColor).HasMaxLength(7).UseCollation("C").IsRequired();

        builder.HasOne<Workspace>().WithOne()
            .HasForeignKey<WorkspaceBranding>(branding => branding.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        // These composite foreign keys protect tenant ownership in SQL.
        // Asset Purpose (LOGO/BANNER) must still be checked by the service.
        builder.HasOne<WorkspaceAsset>().WithMany()
            .HasForeignKey(branding => new { branding.WorkspaceId, branding.LogoAssetId })
            .HasPrincipalKey(asset => new { asset.WorkspaceId, asset.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkspaceAsset>().WithMany()
            .HasForeignKey(branding => new { branding.WorkspaceId, branding.BannerAssetId })
            .HasPrincipalKey(asset => new { asset.WorkspaceId, asset.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
