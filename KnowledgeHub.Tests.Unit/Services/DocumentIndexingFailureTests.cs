
using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Application.Services;
using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Domain.Interfaces;
using Moq;
using Xunit;

namespace KnowledgeHub.Tests.Unit.Services;

public sealed class DocumentIndexingFailureTests
{
    [Fact]
    public async Task Create_WhenEmbeddingFails_DoesNotPersist()
    {
        var repository = new Mock<IKnowledgeDocumentRepository>(
            MockBehavior.Strict);

        var chunker = new Mock<ITextChunker>();
        chunker.Setup(x => x.Chunk("Contenu"))
            .Returns(["Partie"]);

        var generator = new Mock<IChunkEmbeddingGenerator>();

        generator.Setup(x => x.GenerateAsync(
                It.IsAny<IReadOnlyCollection<DocumentChunk>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "Embedding generation failed."));

        var service = CreateService(
            repository.Object,
            chunker.Object,
            generator.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(new CreateDocumentDto
            {
                Title = "Document",
                Content = "Contenu"
            }));

        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Update_WhenEmbeddingFails_PreservesDocument()
    {
        var document = new KnowledgeDocument(
            "Titre initial",
            "Contenu initial");

        document.AddChunk(0, 7, "Ancien");

        var originalChunkId = document.Chunks.Single().Id;

        var repository = new Mock<IKnowledgeDocumentRepository>();

        repository.Setup(x => x.GetByIdAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var chunker = new Mock<ITextChunker>();
        chunker.Setup(x => x.Chunk("Contenu nouveau"))
            .Returns(["Nouveau"]);

        var generator = new Mock<IChunkEmbeddingGenerator>();

        generator.Setup(x => x.GenerateAsync(
                It.IsAny<IReadOnlyCollection<DocumentChunk>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "Embedding generation failed."));

        var service = CreateService(
            repository.Object,
            chunker.Object,
            generator.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateAsync(
                document.Id,
                new UpdateDocumentDto
                {
                    Title = "Titre modifié",
                    Content = "Contenu nouveau"
                }));

        Assert.Equal("Titre initial", document.Title);
        Assert.Equal("Contenu initial", document.Content);

        Assert.Equal(
            originalChunkId,
            document.Chunks.Single().Id);

        repository.Verify(x => x.UpdateIndexedAsync(
                It.IsAny<KnowledgeDocument>(),
                It.IsAny<IReadOnlyCollection<ChunkEmbedding>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        repository.Verify(x => x.UpdateAsync(
                It.IsAny<KnowledgeDocument>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static DocumentService CreateService(
        IKnowledgeDocumentRepository repository,
        ITextChunker chunker,
        IChunkEmbeddingGenerator generator)
    {
        return new DocumentService(
            repository,
            Mock.Of<ICategoryRepository>(),
            Mock.Of<IKnowledgeSourceRepository>(),
            Mock.Of<ITagRepository>(),
            chunker,
            generator);
    }
}
