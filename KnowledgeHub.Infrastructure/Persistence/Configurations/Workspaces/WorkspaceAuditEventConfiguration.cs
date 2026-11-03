using KnowledgeHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KnowledgeHub.Infrastructure.Persistence.Configurations.Workspaces;

public sealed class WorkspaceAuditEventConfiguration :
    IEntityTypeConfiguration<WorkspaceAuditEvent>, IWorkspacePresentationStagedConfiguration
{
    public void Configure(EntityTypeBuilder<WorkspaceAuditEvent> builder)
    {
        builder.ToTable("WorkspaceAuditEvents", table =>
        {
            table.HasCheckConstraint("CK_WorkspaceAuditEvents_EventType", "\"EventType\" BETWEEN 0 AND 15");
            table.HasCheckConstraint("CK_WorkspaceAuditEvents_TargetType", "\"TargetType\" BETWEEN 0 AND 5");
            table.HasCheckConstraint("CK_WorkspaceAuditEvents_Outcome", "\"Outcome\" BETWEEN 0 AND 2");
            table.HasCheckConstraint("CK_WorkspaceAuditEvents_TargetIdNotEmpty",
                "\"TargetId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("CK_WorkspaceAuditEvents_TargetMatchesEvent",
                "((\"EventType\" IN (0,1,2,3,15) AND \"TargetType\" = 0) OR " +
                "(\"EventType\" IN (4,5,6,7) AND \"TargetType\" = 1) OR " +
                "(\"EventType\" IN (8,9,10) AND \"TargetType\" = 2) OR " +
                "(\"EventType\" = 11 AND \"TargetType\" = 3) OR " +
                "(\"EventType\" = 12 AND \"TargetType\" = 4) OR " +
                "(\"EventType\" IN (13,14) AND \"TargetType\" = 5)) " +
                "AND (\"TargetType\" NOT IN (0,3,4) OR \"TargetId\" = \"WorkspaceId\")");
        });
        builder.HasKey(audit => audit.Id);
        builder.Property(audit => audit.EventType).HasConversion<int>().IsRequired();
        builder.Property(audit => audit.TargetType).HasConversion<int>().IsRequired();
        builder.Property(audit => audit.Outcome).HasConversion<int>().IsRequired();
        builder.Property(audit => audit.OccurredAtUtc)
            .HasColumnType("timestamp with time zone").IsRequired();
        builder.HasIndex(audit => new { audit.WorkspaceId, audit.OccurredAtUtc, audit.Id })
            .HasDatabaseName("IX_WorkspaceAuditEvents_WorkspaceOccurredId");
        builder.HasOne<Workspace>().WithMany()
            .HasForeignKey(audit => audit.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany()
            .HasForeignKey(audit => audit.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
