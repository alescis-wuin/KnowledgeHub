namespace KnowledgeHub.Infrastructure.Persistence.Configurations.Workspaces;

/// <summary>
/// KH-009.1.6.2 presentation mappings are staged for isolated validation.
/// The production context must exclude this marker until the accompanying
/// KH-009.1.6.3 migration and snapshot are committed together.
/// </summary>
public interface IWorkspacePresentationStagedConfiguration : IWorkspaceStagedConfiguration { }
