using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Infrastructure.Persistence.Workspaces;
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

    public DbSet<WorkspaceBranding> WorkspaceBrandings => Set<WorkspaceBranding>();

    public DbSet<WorkspaceHomepage> WorkspaceHomepages => Set<WorkspaceHomepage>();

    public DbSet<WorkspaceHomeSection> WorkspaceHomeSections => Set<WorkspaceHomeSection>();

    public DbSet<WorkspaceAsset> WorkspaceAssets => Set<WorkspaceAsset>();

    public DbSet<WorkspaceAuditEvent> WorkspaceAuditEvents => Set<WorkspaceAuditEvent>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(WorkspaceAuditAppendOnlyInterceptor.Instance);
        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        // KH-009.1.6.3: map presentation/audit together with the additive
        // migration and snapshot. No database migration is run at startup.
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(KnowledgeDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
