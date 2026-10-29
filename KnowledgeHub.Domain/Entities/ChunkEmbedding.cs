namespace KnowledgeHub.Domain.Entities;

public class ChunkEmbedding
{
    public Guid Id { get; private set; }

    public Guid ChunkId { get; private set; }

    public float[] Embedding
    {
        get;
        private set;
    }
        = [];

    public DateTime CreatedAt
    {
        get;
        private set;
    }

    public DocumentChunk Chunk
    {
        get;
        private set;
    } = null!;

    private ChunkEmbedding()
    {
    }

    public ChunkEmbedding(
        Guid chunkId,
        float[] embedding)
    {
        Id = Guid.NewGuid();

        ChunkId = chunkId;

        Embedding = embedding;

        CreatedAt =
            DateTime.UtcNow;
    }
}