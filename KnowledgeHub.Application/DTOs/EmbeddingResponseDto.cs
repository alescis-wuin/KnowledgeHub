namespace KnowledgeHub.Application.DTOs;

public class EmbeddingResponseDto
{
    public int Dimensions { get; set; }

    public IReadOnlyCollection<float>
        Embedding
    {
        get;
        set;
    }
    = [];
}