namespace KnowledgeHub.Infrastructure.Persistence.Configurations.Workspaces;

/// <summary>
/// Deliberate migration boundary: staged schema mappings must not be loaded
/// into the production KnowledgeDbContext until KH-009.1.5 creates migrations.
/// </summary>
public interface IWorkspaceStagedConfiguration { }
