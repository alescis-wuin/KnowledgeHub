namespace KnowledgeHub.Domain.Entities;

public class KnowledgeDocument
{
    public Guid Id { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Content { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    public Guid? CategoryId
    {
        get;
        private set;
    }

    public Category? Category
    {
        get;
        private set;
    }

    public Guid? SourceId
    {
        get;
        private set;
    }

    public KnowledgeSource? Source
    {
        get;
        private set;
    }

    public string? Summary
    {
        get;
        private set;
    }

    public string? SearchKeywords
    {
        get;
        private set;
    }

    public void SetSummary(
        string? summary)
    {
        Summary = summary;
    }

    public void SetSearchKeywords(
        string? keywords)
    {
        SearchKeywords = keywords;
    }

    public ICollection<DocumentChunk> Chunks
    {
        get;
        private set;
    }
    = new List<DocumentChunk>();

    public ICollection<KnowledgeDocumentTag> DocumentTags
    {
        get;
        private set;
    }
    = new List<KnowledgeDocumentTag>();

    private KnowledgeDocument()
    {
    }

    public KnowledgeDocument(
        string title,
        string content)
    {
        Id = Guid.NewGuid();

        Update(title, content);

        CreatedAt = DateTime.UtcNow;
    }

    public void Update(
        string title,
        string content)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException(
                "Title is required");

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException(
                "Content is required");

        Title = title.Trim();
        Content = content.Trim();

        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignCategory(
        Guid? categoryId)
    {
        CategoryId = categoryId;
    }

    public void AssignSource(
        Guid? sourceId)
    {
        SourceId = sourceId;
    }

    public void AddTag(
        Tag tag)
    {
        if (DocumentTags.Any(x => x.TagId == tag.Id))
        {
            return;
        }

        DocumentTags.Add(
            new KnowledgeDocumentTag
            {
                DocumentId = Id,
                TagId = tag.Id,
                Tag = tag,
                Document = this
            });
    }

    public void ClearTags()
    {
        DocumentTags.Clear();
    }

	public void ReplaceChunks(
	    IReadOnlyCollection<DocumentChunk> replacements)
	{
	    ArgumentNullException.ThrowIfNull(replacements);

	    if (replacements.Count == 0)
	    {
	        throw new ArgumentException(
	            "A document must have at least one chunk.",
	            nameof(replacements));
	    }

	    var ordered = replacements
	        .OrderBy(chunk => chunk.ChunkIndex)
	        .ToArray();

	    if (ordered.Any(chunk => chunk.DocumentId != Id))
	    {
	        throw new ArgumentException(
	            "All chunks must belong to this document.",
	            nameof(replacements));
	    }

	    if (ordered.Select(chunk => chunk.Id)
	        .Distinct().Count() != ordered.Length)
	    {
	        throw new ArgumentException(
	            "Duplicate chunk identifiers are not allowed.",
	            nameof(replacements));
	    }

	    for (var index = 0; index < ordered.Length; index++)
	    {
	        if (ordered[index].ChunkIndex != index)
	        {
	            throw new ArgumentException(
	                "Chunk indices must be consecutive and start at zero.",
	                nameof(replacements));
	        }
	    }

	    Chunks.Clear();

	    foreach (var chunk in ordered)
	    {
	        Chunks.Add(chunk);
	    }
	}

    public void AddChunk(
        int chunkIndex,
        int tokenCount,
        string content)
    {
        Chunks.Add(
            new DocumentChunk(
                Id,
                chunkIndex,
                tokenCount,
                content));
    }

    public void ClearChunks()
    {
        Chunks.Clear();
    }
}
