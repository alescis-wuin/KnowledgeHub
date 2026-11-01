using KnowledgeHub.Application.Verification;
using Xunit;

namespace KnowledgeHub.Tests.Unit.Verification;

public sealed class EvidenceRetrievalSnapshotTests
{
    private static readonly Guid DocumentId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid ChunkId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

    [Fact]
    public void Constructor_CopiesAllowlistAndDocumentVersions()
    {
        var chunk = new EvidenceSnapshot(DocumentId, ChunkId, "Texte stable");
        var chunks = new List<EvidenceSnapshot> { chunk };
        var versions = new Dictionary<Guid, string> { [DocumentId] = new string('a', 64) };

        var snapshot = new EvidenceRetrievalSnapshot(chunks, versions);
        chunks.Clear();
        versions.Clear();

        Assert.Single(snapshot.AllowedChunks);
        Assert.Equal(chunk.Content, snapshot.AllowedChunks[ChunkId].Content);
        Assert.Equal(new string('a', 64), snapshot.DocumentContentSha256[DocumentId]);
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<Guid, EvidenceSnapshot>)snapshot.AllowedChunks).Clear());
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<Guid, string>)snapshot.DocumentContentSha256).Clear());
    }

    [Fact]
    public void Constructor_RejectsDuplicateOrOrphanEvidence()
    {
        var chunk = new EvidenceSnapshot(DocumentId, ChunkId, "Texte");
        var versions = new Dictionary<Guid, string> { [DocumentId] = new string('a', 64) };

        Assert.Throws<ArgumentException>(() =>
            new EvidenceRetrievalSnapshot([chunk, chunk], versions));
        Assert.Throws<ArgumentException>(() =>
            new EvidenceRetrievalSnapshot([chunk], new Dictionary<Guid, string>()));
        Assert.Throws<ArgumentException>(() =>
            new EvidenceRetrievalSnapshot([chunk], new Dictionary<Guid, string>
            {
                [DocumentId] = new string('A', 64)
            }));
    }

    [Fact]
    public void Constructor_RejectsUnrelatedDocumentVersion()
    {
        var chunk = new EvidenceSnapshot(DocumentId, ChunkId, "Texte");
        var versions = new Dictionary<Guid, string>
        {
            [DocumentId] = new string('a', 64),
            [Guid.NewGuid()] = new string('b', 64)
        };

        Assert.Throws<ArgumentException>(() =>
            new EvidenceRetrievalSnapshot([chunk], versions));
    }
}
