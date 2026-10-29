namespace KnowledgeHub.Application.Interfaces;

public interface ITextChunker
{
    IReadOnlyCollection<string> Chunk(
        string content);
}