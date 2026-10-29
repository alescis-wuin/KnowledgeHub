using KnowledgeHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KnowledgeHub.Infrastructure.Persistence.Configurations;

public sealed class EvaluationDatasetConfiguration
    : IEntityTypeConfiguration<EvaluationDataset>
{
    public void Configure(
        EntityTypeBuilder<EvaluationDataset> builder)
    {
        builder.ToTable("EvaluationDatasets");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.SourceType)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasMany(x => x.Questions)
            .WithOne(x => x.Dataset)
            .HasForeignKey(x => x.DatasetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}