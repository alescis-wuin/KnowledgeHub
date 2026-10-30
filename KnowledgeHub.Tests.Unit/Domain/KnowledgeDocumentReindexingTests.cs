
using KnowledgeHub.Domain.Entities;
using Xunit;

namespace KnowledgeHub.Tests.Unit.Domain;

public sealed class KnowledgeDocumentReindexingTests
{
    [Fact]
    public void ReplaceChunks_ReplacesAndOrdersChunks()
    {
        var document = new KnowledgeDocument(
            "Document",
            "Ancien contenu");

        document.AddChunk(0, 6, "Ancien");

        var oldChunkId = document.Chunks.Single().Id;

        var first = new DocumentChunk(
            document.Id, 0, 7, "Premier");

        var second = new DocumentChunk(
            document.Id, 1, 8, "Deuxième");

        document.ReplaceChunks([second, first]);

        Assert.Equal(2, document.Chunks.Count);

        var chunks = document.Chunks.ToArray();

        Assert.Same(first, chunks[0]);
        Assert.Same(second, chunks[1]);

        Assert.DoesNotContain(
            document.Chunks,
            chunk => chunk.Id == oldChunkId);
    }

    [Fact]
    public void ReplaceChunks_RejectsForeignDocumentChunks()
    {
        var document = new KnowledgeDocument(
            "Document",
            "Contenu");

        document.AddChunk(0, 6, "Ancien");

        var oldChunkId = document.Chunks.Single().Id;

        var foreignChunk = new DocumentChunk(
            Guid.NewGuid(), 0, 7, "Etranger");

        Assert.Throws<ArgumentException>(
            () => document.ReplaceChunks([foreignChunk]));

        Assert.Equal(
            oldChunkId,
            document.Chunks.Single().Id);
    }

    [Fact]
    public void ReplaceChunks_RejectsInvalidIndices()
    {
        var document = new KnowledgeDocument(
            "Document",
            "Contenu");

        var chunk = new DocumentChunk(
            document.Id, 2, 7, "Contenu");

        Assert.Throws<ArgumentException>(
            () => document.ReplaceChunks([chunk]));

        Assert.Empty(document.Chunks);
    }

    [Fact]
    public void ReplaceChunks_RejectsEmptyCollection()
    {
        var document = new KnowledgeDocument(
            "Document",
            "Contenu");

        Assert.Throws<ArgumentException>(
            () => document.ReplaceChunks([]));
    }
}
