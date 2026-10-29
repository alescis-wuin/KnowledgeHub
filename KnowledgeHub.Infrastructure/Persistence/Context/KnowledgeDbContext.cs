using KnowledgeHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeHub.Infrastructure.Persistence.Context;

public class KnowledgeDbContext : DbContext
{
    public KnowledgeDbContext(
        DbContextOptions<KnowledgeDbContext> options)
        : base(options)
    {
    }

    public DbSet<KnowledgeDocument> Documents =>
        Set<KnowledgeDocument>();

    public DbSet<DocumentChunk> DocumentChunks =>
        Set<DocumentChunk>();

    public DbSet<Category> Categories =>
    Set<Category>();

    public DbSet<Tag> Tags =>
        Set<Tag>();

    public DbSet<KnowledgeSource> Sources =>
        Set<KnowledgeSource>();

    public DbSet<ChunkEmbedding>
        ChunkEmbeddings =>
            Set<ChunkEmbedding>();

    public DbSet<EvaluationDataset> EvaluationDatasets =>
    Set<EvaluationDataset>();

    public DbSet<EvaluationQuestion> EvaluationQuestions =>
        Set<EvaluationQuestion>();

    public DbSet<EvaluationReferenceDocument> EvaluationReferenceDocuments =>
        Set<EvaluationReferenceDocument>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension(
            "vector");
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(KnowledgeDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}