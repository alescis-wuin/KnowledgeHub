using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Infrastructure.Persistence.Configurations.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeHub.Infrastructure.Persistence.Workspaces;

/// <summary>
/// Temporary model-only DbContext for PostgreSQL Testcontainers. NOT registered
/// in API DI. Use only on a fresh isolated database with EnsureCreatedAsync.
/// KH-009.1.5 integrates these configurations into KnowledgeDbContext together
/// with an additive, reviewed migration, then retires this staging context.
/// </summary>
public sealed class WorkspaceSchemaValidationContext : DbContext
{
    public WorkspaceSchemaValidationContext(
        DbContextOptions<WorkspaceSchemaValidationContext> options) : base(options) { }

    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();
    public DbSet<WorkspaceInvitation> WorkspaceInvitations => Set<WorkspaceInvitation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AppUserConfiguration).Assembly,
            type => typeof(IWorkspaceStagedConfiguration).IsAssignableFrom(type));
        base.OnModelCreating(modelBuilder);
    }
}
