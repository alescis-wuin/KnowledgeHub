using System.Text.Json;
using KnowledgeHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KnowledgeHub.Infrastructure.Persistence.Configurations;

public sealed class ChunkEmbeddingConfiguration
    : IEntityTypeConfiguration<ChunkEmbedding>
{
    public void Configure(
        EntityTypeBuilder<ChunkEmbedding> builder)
    {
        builder.ToTable(
            "ChunkEmbeddings");

        builder.HasKey(
            x => x.Id);

        builder.HasIndex(
                x => x.ChunkId)
            .IsUnique();

        builder.Property(
                x => x.Embedding)
            .HasConversion(
                embedding =>
                    JsonSerializer.Serialize(
                        embedding,
                        (JsonSerializerOptions?)null),

                json =>
                    JsonSerializer.Deserialize<float[]>(
                        json,
                        (JsonSerializerOptions?)null)
                    ?? Array.Empty<float>())
            .Metadata.SetValueComparer(
                new ValueComparer<float[]>(
                    (a, b) =>
                        a!.SequenceEqual(b!),

                    a =>
                        a.Aggregate(
                            0,
                            (current, value) =>
                                HashCode.Combine(
                                    current,
                                    value)),

                    a =>
                        a.ToArray()));

        builder.Property(
                x => x.Embedding)
            .HasColumnType(
                "text");
    }
}