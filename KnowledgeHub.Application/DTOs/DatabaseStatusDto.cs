namespace KnowledgeHub.Application.DTOs
{
    public sealed record DatabaseStatusDto(
        bool Connected,
        DateTime Timestamp);
}
