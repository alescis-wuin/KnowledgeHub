using KnowledgeHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KnowledgeHub.Infrastructure.Persistence.Configurations.Workspaces;

public sealed class WorkspaceInvitationConfiguration :
    IEntityTypeConfiguration<WorkspaceInvitation>, IWorkspaceStagedConfiguration
{
    public void Configure(EntityTypeBuilder<WorkspaceInvitation> builder)
    {
        builder.ToTable("WorkspaceInvitations", table =>
        {
            table.HasCheckConstraint("CK_WorkspaceInvitations_TokenHash",
                "\"TokenHash\" ~ '^[0-9a-f]{64}$'");
            table.HasCheckConstraint("CK_WorkspaceInvitations_EmailNotBlank",
                "length(btrim(\"EmailNormalized\")) > 3 " +
                "AND position('@' in \"EmailNormalized\") > 1");
            table.HasCheckConstraint("CK_WorkspaceInvitations_RoleNoOwner",
                "\"Role\" BETWEEN 0 AND 3");
            table.HasCheckConstraint("CK_WorkspaceInvitations_ExpiresLater",
                "\"ExpiresAtUtc\" > \"CreatedAtUtc\" AND " +
                "\"ExpiresAtUtc\" <= \"CreatedAtUtc\" + INTERVAL '14 days'");
            table.HasCheckConstraint("CK_WorkspaceInvitations_NotAcceptedAndRevoked",
                "\"AcceptedAtUtc\" IS NULL OR \"RevokedAtUtc\" IS NULL");
            table.HasCheckConstraint("CK_WorkspaceInvitations_AcceptedByConsistency",
                "(\"AcceptedAtUtc\" IS NULL) = (\"AcceptedByUserId\" IS NULL)");
            table.HasCheckConstraint("CK_WorkspaceInvitations_VersionPositive", "\"Version\" >= 1");
        });
        builder.HasKey(invitation => invitation.Id);
        builder.Property(invitation => invitation.EmailNormalized)
            .HasMaxLength(320).UseCollation("C").IsRequired();
        builder.Property(invitation => invitation.Role).HasConversion<int>().IsRequired();
        builder.Property(invitation => invitation.TokenHash)
            .HasMaxLength(64).UseCollation("C").IsRequired();
        builder.Property(invitation => invitation.CreatedAtUtc)
            .HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(invitation => invitation.ExpiresAtUtc)
            .HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(invitation => invitation.AcceptedAtUtc)
            .HasColumnType("timestamp with time zone");
        builder.Property(invitation => invitation.RevokedAtUtc)
            .HasColumnType("timestamp with time zone");
        builder.Property(invitation => invitation.Version).IsRequired().IsConcurrencyToken();
        builder.HasIndex(invitation => invitation.TokenHash)
            .IsUnique().HasDatabaseName("UX_WorkspaceInvitations_TokenHash");
        builder.HasIndex(invitation => new { invitation.WorkspaceId, invitation.EmailNormalized });
        builder.HasOne<Workspace>().WithMany()
            .HasForeignKey(invitation => invitation.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany()
            .HasForeignKey(invitation => invitation.InviterUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany()
            .HasForeignKey(invitation => invitation.AcceptedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
