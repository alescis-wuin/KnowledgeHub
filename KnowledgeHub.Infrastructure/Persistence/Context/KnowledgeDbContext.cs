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

    public DbSet<AppUser> AppUsers => Set<AppUser>();

    public DbSet<Workspace> Workspaces => Set<Workspace>();

    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();

    public DbSet<WorkspaceInvitation> WorkspaceInvitations => Set<WorkspaceInvitation>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        // KH-009.1.5: include the validated workspace mappings together with
        // their additive migration and EF Core model snapshot.
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(KnowledgeDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
