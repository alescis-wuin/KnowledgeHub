using KnowledgeHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KnowledgeHub.Infrastructure.Persistence.Configurations.Workspaces;

public sealed class WorkspaceAssetConfiguration :
    IEntityTypeConfiguration<WorkspaceAsset>, IWorkspacePresentationStagedConfiguration
{
    public void Configure(EntityTypeBuilder<WorkspaceAsset> builder)
    {
        builder.ToTable("WorkspaceAssets", table =>
        {
            table.HasCheckConstraint("CK_WorkspaceAssets_Purpose", "\"Purpose\" BETWEEN 0 AND 1");
            table.HasCheckConstraint("CK_WorkspaceAssets_ContentType",
                "\"ContentType\" IN ('image/png','image/jpeg','image/webp')");
            table.HasCheckConstraint("CK_WorkspaceAssets_StorageKey",
                "\"StorageKey\" ~ '^[0-9a-f]{64}$'");
            table.HasCheckConstraint("CK_WorkspaceAssets_Sha256",
                "\"Sha256\" ~ '^[0-9a-f]{64}$'");
            table.HasCheckConstraint("CK_WorkspaceAssets_Length",
                "\"Length\" >= 1 AND ((\"Purpose\" = 0 AND \"Length\" <= 5242880) OR " +
                "(\"Purpose\" = 1 AND \"Length\" <= 10485760))");
            table.HasCheckConstraint("CK_WorkspaceAssets_Dimensions",
                "\"PixelWidth\" BETWEEN 1 AND 8192 AND \"PixelHeight\" BETWEEN 1 AND 8192 " +
                "AND \"PixelWidth\"::bigint * \"PixelHeight\"::bigint <= 16777216");
        });
        builder.HasKey(asset => asset.Id);
        builder.HasAlternateKey(asset => new { asset.WorkspaceId, asset.Id })
            .HasName("AK_WorkspaceAssets_WorkspaceId_Id");
        builder.Property(asset => asset.Purpose).HasConversion<int>().IsRequired();
        builder.Property(asset => asset.ContentType).HasMaxLength(16).UseCollation("C").IsRequired();
        builder.Property(asset => asset.StorageKey).HasMaxLength(64).UseCollation("C").IsRequired();
        builder.Property(asset => asset.Sha256).HasMaxLength(64).UseCollation("C").IsRequired();
        builder.Property(asset => asset.Length).IsRequired();
        builder.Property(asset => asset.PixelWidth).IsRequired();
        builder.Property(asset => asset.PixelHeight).IsRequired();
        builder.Property(asset => asset.CreatedAtUtc)
            .HasColumnType("timestamp with time zone").IsRequired();
        builder.HasIndex(asset => new { asset.WorkspaceId, asset.StorageKey })
            .IsUnique().HasDatabaseName("UX_WorkspaceAssets_WorkspaceStorageKey");
        builder.HasOne<Workspace>().WithMany()
            .HasForeignKey(asset => asset.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany()
            .HasForeignKey(asset => asset.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
