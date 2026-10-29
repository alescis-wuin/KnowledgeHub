using KnowledgeHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KnowledgeHub.Infrastructure.Persistence.Configurations;

public class KnowledgeDocumentTagConfiguration
    : IEntityTypeConfiguration<KnowledgeDocumentTag>
{
    public void Configure(
        EntityTypeBuilder<KnowledgeDocumentTag> builder)
    {
        builder.ToTable("KnowledgeDocumentTags");

        builder.HasKey(x =>
            new
            {
                x.DocumentId,
                x.TagId
            });

        builder.HasOne(x => x.Document)
            .WithMany(x => x.DocumentTags)
            .HasForeignKey(x => x.DocumentId);

        builder.HasOne(x => x.Tag)
            .WithMany(x => x.DocumentTags)
            .HasForeignKey(x => x.TagId);
    }
}