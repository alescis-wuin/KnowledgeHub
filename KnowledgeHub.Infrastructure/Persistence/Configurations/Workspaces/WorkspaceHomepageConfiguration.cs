using KnowledgeHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KnowledgeHub.Infrastructure.Persistence.Configurations.Workspaces;

public sealed class WorkspaceHomepageConfiguration :
    IEntityTypeConfiguration<WorkspaceHomepage>, IWorkspacePresentationStagedConfiguration
{
    public void Configure(EntityTypeBuilder<WorkspaceHomepage> builder)
    {
        builder.ToTable("WorkspaceHomepages", table =>
        {
            table.HasCheckConstraint("CK_WorkspaceHomepages_VersionPositive", "\"Version\" >= 1");
            table.HasCheckConstraint("CK_WorkspaceHomepages_HeaderTitleNotBlank",
                "length(btrim(\"HeaderTitle\")) > 0");
        });
        builder.HasKey(homepage => homepage.WorkspaceId);
        builder.Property(homepage => homepage.HeaderTitle).HasMaxLength(150).IsRequired();
        builder.Property(homepage => homepage.Subtitle).HasMaxLength(300);
        builder.Property(homepage => homepage.WelcomeMessage).HasMaxLength(2000);
        builder.Property(homepage => homepage.Version).IsRequired().IsConcurrencyToken();
        builder.HasOne<Workspace>().WithOne()
            .HasForeignKey<WorkspaceHomepage>(homepage => homepage.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
