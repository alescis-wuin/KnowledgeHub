using KnowledgeHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace KnowledgeHub.Infrastructure.Persistence.Configurations.Workspaces;

public sealed class WorkspaceHomeSectionConfiguration :
    IEntityTypeConfiguration<WorkspaceHomeSection>, IWorkspacePresentationStagedConfiguration
{
    public void Configure(EntityTypeBuilder<WorkspaceHomeSection> builder)
    {
        builder.ToTable("WorkspaceHomeSections", table =>
        {
            table.HasCheckConstraint("CK_WorkspaceHomeSections_ValidType", "\"Type\" BETWEEN 0 AND 2");
            table.HasCheckConstraint("CK_WorkspaceHomeSections_ValidOrder", "\"SortOrder\" BETWEEN 0 AND 19");
            table.HasCheckConstraint("CK_WorkspaceHomeSections_TitleNotBlank", "length(btrim(\"Title\")) > 0");
            table.HasCheckConstraint("CK_WorkspaceHomeSections_LinksShape",
                "jsonb_typeof(\"LinksJson\") = 'array'");
            // CASE ensures malformed jsonb shapes fail the check without
            // attempting jsonb_array_length on a scalar/object.
            table.HasCheckConstraint("CK_WorkspaceHomeSections_ContentByType",
                "CASE WHEN jsonb_typeof(\"LinksJson\") <> 'array' THEN false ELSE " +
                "(\"Type\" = 0 AND \"Body\" IS NOT NULL AND " +
                "length(btrim(\"Body\")) > 0 AND jsonb_array_length(\"LinksJson\") = 0) OR " +
                "(\"Type\" = 1 AND \"Body\" IS NULL AND " +
                "jsonb_array_length(\"LinksJson\") BETWEEN 1 AND 10) OR " +
                "(\"Type\" = 2 AND \"Body\" IS NULL AND " +
                "jsonb_array_length(\"LinksJson\") = 0) END");
        });
        builder.HasKey(section => section.Id);
        builder.Property(section => section.Type).HasConversion<int>().IsRequired();
        builder.Property(section => section.Title).HasMaxLength(150).IsRequired();
        builder.Property(section => section.Body).HasMaxLength(4000);
        builder.Property(section => section.SortOrder).IsRequired();
        builder.HasIndex(section => new { section.WorkspaceId, section.SortOrder })
            .IsUnique().HasDatabaseName("UX_WorkspaceHomeSections_WorkspaceOrder");

        var converter = new ValueConverter<IReadOnlyList<WorkspaceHomeLink>, string>(
            links => WorkspaceHomeLinksJson.Serialize(links),
            json => WorkspaceHomeLinksJson.Deserialize(json));
        var comparer = new ValueComparer<IReadOnlyList<WorkspaceHomeLink>>(
            (left, right) => WorkspaceHomeLinksJson.Equivalent(left, right),
            links => WorkspaceHomeLinksJson.Hash(links),
            links => WorkspaceHomeLinksJson.Snapshot(links));
        builder.Property(section => section.Links)
            .HasField("_links")
            .UsePropertyAccessMode(Microsoft.EntityFrameworkCore.PropertyAccessMode.Field)
            .HasConversion(converter, comparer)
            .HasColumnName("LinksJson")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.HasOne<WorkspaceHomepage>().WithMany(homepage => homepage.Sections)
            .HasForeignKey(section => section.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
