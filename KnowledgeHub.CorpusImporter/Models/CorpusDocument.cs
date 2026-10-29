namespace KnowledgeHub.CorpusImporter.Models;

public class CorpusDocument
{
    public Guid Id { get; set; }

    public string Title { get; set; }
        = string.Empty;

    public string Summary { get; set; }
        = string.Empty;

    public string Content { get; set; }
        = string.Empty;

    public string SearchKeywords { get; set; }
        = string.Empty;

    public string CategoryName { get; set; }
        = string.Empty;

    public string SourceName { get; set; }
        = string.Empty;

    public List<string> TagNames { get; set; }
        = [];

    public DateTime CreatedAt { get; set; }
}