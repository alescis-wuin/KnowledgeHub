namespace KnowledgeHub.CorpusImporter.Models;

public class CorpusSource
{
    public Guid Id { get; set; }

    public string Name { get; set; }
        = string.Empty;

    public string Url { get; set; }
        = string.Empty;
}