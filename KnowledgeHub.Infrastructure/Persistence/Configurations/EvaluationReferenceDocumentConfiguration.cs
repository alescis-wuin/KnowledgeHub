using KnowledgeHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KnowledgeHub.Infrastructure.Persistence.Configurations;

public sealed class EvaluationReferenceDocumentConfiguration
    : IEntityTypeConfiguration<EvaluationReferenceDocument>
{
    public void Configure(
        EntityTypeBuilder<EvaluationReferenceDocument> builder)
    {
        builder.ToTable("EvaluationReferenceDocuments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ExternalDocumentId)
            .HasMaxLength(500)
            .IsRequired();

        builder.HasOne(x => x.EvaluationQuestion)
            .WithMany(x => x.ReferenceDocuments)
            .HasForeignKey(x => x.EvaluationQuestionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}