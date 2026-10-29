namespace KnowledgeHub.Domain.Entities;

public class DocumentChunk
{
    public Guid Id { get; private set; }

    public Guid DocumentId { get; private set; }

    public int ChunkIndex { get; private set; }

    public int TokenCount { get; private set; }

    public string Content { get; private set; }
        = string.Empty;

    public DateTime CreatedAt
    {
        get;
        private set;
    }

    public KnowledgeDocument Document { get; private set; }
        = null!;

    private DocumentChunk()
    {
    }

    public DocumentChunk(
        Guid documentId,
        int chunkIndex,
        int tokenCount,
        string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException(
                "Chunk content is required");
        }

        if (chunkIndex < 0)
        {
            throw new ArgumentException(
                "Chunk index cannot be negative");
        }

        if (tokenCount < 0)
        {
            throw new ArgumentException(
                "Token count cannot be negative");
        }

        Id = Guid.NewGuid();

        DocumentId = documentId;

        ChunkIndex = chunkIndex;

        TokenCount = tokenCount;

        Content = content.Trim();

        CreatedAt = DateTime.UtcNow;
    }
}