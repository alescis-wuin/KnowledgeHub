using KnowledgeHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KnowledgeHub.Infrastructure.Persistence.Configurations.Workspaces;

public sealed class WorkspaceConfiguration :
    IEntityTypeConfiguration<Workspace>, IWorkspaceStagedConfiguration
{
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.ToTable("Workspaces", table =>
        {
            table.HasCheckConstraint("CK_Workspaces_SlugFormat",
                "length(\"SlugNormalized\") BETWEEN 3 AND 80 " +
                "AND \"SlugNormalized\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
            table.HasCheckConstraint("CK_Workspaces_VersionPositive", "\"Version\" >= 1");
            table.HasCheckConstraint("CK_Workspaces_NameNotBlank",
                "length(btrim(\"Name\")) > 0");
            table.HasCheckConstraint("CK_Workspaces_ArchivedAfterCreated",
                "\"ArchivedAtUtc\" IS NULL OR \"ArchivedAtUtc\" >= \"CreatedAtUtc\"");
        });
        builder.HasKey(workspace => workspace.Id);
        builder.Property(workspace => workspace.Name).HasMaxLength(150).IsRequired();
        builder.Property(workspace => workspace.SlugNormalized)
            .HasMaxLength(80).UseCollation("C").IsRequired();
        builder.Property(workspace => workspace.Description).HasMaxLength(1000);
        builder.Property(workspace => workspace.CreatedByUserId).IsRequired();
        builder.Property(workspace => workspace.CreatedAtUtc)
            .HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(workspace => workspace.ArchivedAtUtc)
            .HasColumnType("timestamp with time zone");
        builder.Property(workspace => workspace.Version)
            .IsRequired().IsConcurrencyToken();
        builder.Property(workspace => workspace.IsVerifiedOrganization).IsRequired();
        builder.HasIndex(workspace => workspace.SlugNormalized)
            .IsUnique().HasDatabaseName("UX_Workspaces_SlugNormalized");
        builder.HasOne<AppUser>().WithMany()
            .HasForeignKey(workspace => workspace.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
