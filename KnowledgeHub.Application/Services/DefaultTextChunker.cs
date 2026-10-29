using KnowledgeHub.Application.Interfaces;

namespace KnowledgeHub.Application.Services;

public class DefaultTextChunker : ITextChunker
{
    public IReadOnlyCollection<string> Chunk(
        string content)
    {
        const int maxSize = 500;

        return content
            .Chunk(maxSize)
            .Select(x => new string(x))
            .ToList();
    }
}