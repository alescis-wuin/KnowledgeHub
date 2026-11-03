using KnowledgeHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KnowledgeHub.Infrastructure.Persistence.Configurations.Workspaces;

public sealed class WorkspaceMemberConfiguration :
    IEntityTypeConfiguration<WorkspaceMember>, IWorkspaceStagedConfiguration
{
    public void Configure(EntityTypeBuilder<WorkspaceMember> builder)
    {
        builder.ToTable("WorkspaceMembers", table =>
        {
            table.HasCheckConstraint("CK_WorkspaceMembers_ValidRole", "\"Role\" BETWEEN 0 AND 4");
            table.HasCheckConstraint("CK_WorkspaceMembers_ValidStatus", "\"Status\" BETWEEN 0 AND 2");
            table.HasCheckConstraint("CK_WorkspaceMembers_OwnerMustBeActive",
                "\"Role\" <> 4 OR \"Status\" = 1");
            table.HasCheckConstraint("CK_WorkspaceMembers_VersionPositive", "\"Version\" >= 1");
        });
        builder.HasKey(member => new { member.WorkspaceId, member.UserId });
        builder.Property(member => member.Role).HasConversion<int>().IsRequired();
        builder.Property(member => member.Status).HasConversion<int>().IsRequired();
        builder.Property(member => member.JoinedAtUtc)
            .HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(member => member.Version).IsRequired().IsConcurrencyToken();
        builder.HasIndex(member => member.UserId);
        builder.HasOne<Workspace>().WithMany()
            .HasForeignKey(member => member.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany()
            .HasForeignKey(member => member.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
