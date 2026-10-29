namespace KnowledgeHub.Domain.Models;

public class KnowledgeStatistics
{
    public int DocumentsCount { get; set; }

    public int CategoriesCount { get; set; }

    public int SourcesCount { get; set; }

    public int TagsCount { get; set; }

    public int ChunksCount { get; set; }
}