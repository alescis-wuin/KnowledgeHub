using KnowledgeHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KnowledgeHub.Infrastructure.Persistence.Configurations;

public sealed class EvaluationQuestionConfiguration
    : IEntityTypeConfiguration<EvaluationQuestion>
{
    public void Configure(
        EntityTypeBuilder<EvaluationQuestion> builder)
    {
        builder.ToTable("EvaluationQuestions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Question)
            .IsRequired();

        builder.Property(x => x.ExpectedAnswer);

        builder.Property(x => x.Difficulty)
            .HasMaxLength(50);

        builder.Property(x => x.ExternalQuestionId)
            .HasMaxLength(100)
            .IsRequired();
    }
}