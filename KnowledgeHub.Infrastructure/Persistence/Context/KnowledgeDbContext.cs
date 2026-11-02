using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Infrastructure.Persistence.Configurations.Workspaces;
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

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        // Workspace maps are staged until KH-009.1.5 updates the snapshot.
        // EF 9+ rejects MigrateAsync if model and migrations disagree.
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(KnowledgeDbContext).Assembly,
            type => !typeof(IWorkspaceStagedConfiguration).IsAssignableFrom(type));
        base.OnModelCreating(modelBuilder);
    }
}
