namespace KnowledgeHub.Application.DTOs;

/// <summary>Outcome of a corpus document upsert, independent of HTTP status.</summary>
public enum ImportDocumentOutcome
{
    Created,
    UpdatedContent,
    UpdatedMetadata,
    RepairedIndex,
    Unchanged
}

/// <summary>Identity and exact action performed by the import endpoint.</summary>
public sealed record ImportDocumentResultDto(
    DocumentDto Document,
    ImportDocumentOutcome Outcome);
