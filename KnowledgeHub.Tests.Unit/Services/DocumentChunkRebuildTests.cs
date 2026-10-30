
using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Application.Services;
using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Domain.Interfaces;
using Moq;
using Xunit;

namespace KnowledgeHub.Tests.Unit.Services;

public sealed class DocumentChunkRebuildTests
{
    private readonly Mock<IKnowledgeDocumentRepository> _repository = new();
    private readonly Mock<ITextChunker> _chunker = new();
	private readonly Mock<IChunkEmbeddingGenerator> _embeddingGenerator = new();


	private DocumentService CreateService()
	{
	    _embeddingGenerator
	        .Setup(x => x.GenerateAsync(
	            It.IsAny<IReadOnlyCollection<DocumentChunk>>(),
	            It.IsAny<CancellationToken>()))
	        .Returns((
	            IReadOnlyCollection<DocumentChunk> chunks,
	            CancellationToken _) =>
	            Task.FromResult<IReadOnlyList<ChunkEmbedding>>(
	                chunks.Select(chunk => new ChunkEmbedding(
	                    chunk.Id,
	                    new float[] { 1f, 0f }))
	                .ToArray()));

	    var invalidator = new Mock<IVectorIndexCacheInvalidator>();
	    invalidator.Setup(x => x.InvalidateAsync())
	        .Returns(Task.CompletedTask);

	    return new DocumentService(
	        _repository.Object,
	        Mock.Of<ICategoryRepository>(),
	        Mock.Of<IKnowledgeSourceRepository>(),
	        Mock.Of<ITagRepository>(),
	        _chunker.Object,
	        _embeddingGenerator.Object,
	        invalidator.Object);
	}

    [Fact]
    public async Task ContentChanged_RebuildsChunks()
    {
        var document = new KnowledgeDocument(
            "Titre initial",
            "Contenu ancien");

        document.AddChunk(0, 6, "Ancien");

        var oldChunkId = document.Chunks.Single().Id;

        _repository
            .Setup(x => x.GetByIdAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        _chunker
            .Setup(x => x.Chunk("Contenu nouveau"))
            .Returns(new[]
            {
                "  Partie A  ",
                " ",
                "Partie B"
            });

        var service = CreateService();

        var result = await service.UpdateAsync(
            document.Id,
            new UpdateDocumentDto
            {
                Title = "Titre initial",
                Content = "Contenu nouveau"
            });

        Assert.NotNull(result);

        var chunks = document.Chunks
            .OrderBy(x => x.ChunkIndex)
            .ToArray();

        Assert.Equal(2, chunks.Length);
        Assert.Equal("Partie A", chunks[0].Content);
        Assert.Equal("Partie B", chunks[1].Content);
        Assert.DoesNotContain(
            chunks,
            x => x.Id == oldChunkId);

		_repository.Verify(
		    x => x.UpdateIndexedAsync(
		        document,
		        It.Is<IReadOnlyCollection<ChunkEmbedding>>(
		            embeddings => embeddings.Count == 2),
		        It.IsAny<CancellationToken>()),
		    Times.Once);

		_embeddingGenerator.Verify(
		    x => x.GenerateAsync(
		        It.Is<IReadOnlyCollection<DocumentChunk>>(
		            chunks => chunks.Count == 2),
		        It.IsAny<CancellationToken>()),
		    Times.Once);
    }

    [Fact]
    public async Task TitleOnlyChange_PreservesChunks()
    {
        var document = new KnowledgeDocument(
            "Ancien titre",
            "Contenu inchangé");

        document.AddChunk(
            0,
            17,
            "Contenu inchangé");

        var originalChunkId =
            document.Chunks.Single().Id;

        _repository
            .Setup(x => x.GetByIdAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var service = CreateService();

        var result = await service.UpdateAsync(
            document.Id,
            new UpdateDocumentDto
            {
                Title = "Nouveau titre",
                Content = "Contenu inchangé"
            });

        Assert.NotNull(result);
        Assert.Equal("Nouveau titre", result.Title);
        Assert.Single(document.Chunks);

        Assert.Equal(
            originalChunkId,
            document.Chunks.Single().Id);

        _chunker.Verify(
            x => x.Chunk(It.IsAny<string>()),
            Times.Never);

		_embeddingGenerator.Verify(
		    x => x.GenerateAsync(
		        It.IsAny<IReadOnlyCollection<DocumentChunk>>(),
		        It.IsAny<CancellationToken>()),
		    Times.Never);
    }
}
