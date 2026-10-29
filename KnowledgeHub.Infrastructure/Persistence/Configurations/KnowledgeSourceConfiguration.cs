using KnowledgeHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KnowledgeHub.Infrastructure.Persistence.Configurations;

public class KnowledgeSourceConfiguration
    : IEntityTypeConfiguration<KnowledgeSource>
{
    public void Configure(
        EntityTypeBuilder<KnowledgeSource> builder)
    {
        builder.ToTable("KnowledgeSources");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Url)
            .HasMaxLength(2000);
    }
}
