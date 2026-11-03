using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Infrastructure.Persistence.Configurations.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeHub.Infrastructure.Persistence.Workspaces;

/// <summary>
/// Temporary DbContext for PostgreSQL Testcontainers. It is NOT registered
/// in production DI. EnsureCreatedAsync is permitted only on a fresh, isolated
/// test database; never on the developer's or production database.
/// </summary>
public sealed class WorkspaceSchemaValidationContext : DbContext
{
    public WorkspaceSchemaValidationContext(
        DbContextOptions<WorkspaceSchemaValidationContext> options) : base(options) { }

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Presentation marker derives from IWorkspaceStagedConfiguration;
        // this isolated context intentionally maps all nine workspace types.
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AppUserConfiguration).Assembly,
            type => typeof(IWorkspaceStagedConfiguration).IsAssignableFrom(type));
        base.OnModelCreating(modelBuilder);
    }
}
