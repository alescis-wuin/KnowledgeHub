namespace KnowledgeHub.CorpusImporter.Models;

public class CorpusRoot
{
    public List<CorpusCategory> Categories { get; set; }
        = [];

    public List<CorpusSource> Sources { get; set; }
        = [];

    public List<CorpusTag> Tags { get; set; }
        = [];

    public List<CorpusDocument> Documents { get; set; }
        = [];
}