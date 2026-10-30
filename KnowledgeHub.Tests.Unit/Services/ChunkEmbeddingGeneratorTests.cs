
using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Application.Services;
using KnowledgeHub.Domain.Entities;
using Moq;
using Xunit;

namespace KnowledgeHub.Tests.Unit.Services;

public sealed class ChunkEmbeddingGeneratorTests
{
    private static DocumentChunk CreateChunk(
        string content,
        int index = 0)
    {
        return new DocumentChunk(
            Guid.NewGuid(),
            index,
            content.Length,
            content);
    }

    [Fact]
    public async Task GenerateAsync_CreatesEmbeddingForEachChunk()
    {
        var first = CreateChunk("Premier", 0);
        var second = CreateChunk("Deuxième", 1);

        var embeddingService = new Mock<IEmbeddingService>();

        embeddingService
            .Setup(x => x.GenerateEmbeddingAsync(
                "Premier",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([1f, 0f]);

        embeddingService
            .Setup(x => x.GenerateEmbeddingAsync(
                "Deuxième",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([0f, 1f]);

        var generator = new ChunkEmbeddingGenerator(
            embeddingService.Object);

        var result = await generator.GenerateAsync(
            [first, second]);

        Assert.Collection(
            result,
            embedding =>
            {
                Assert.Equal(first.Id, embedding.ChunkId);
                Assert.Equal([1f, 0f], embedding.Embedding);
            },
            embedding =>
            {
                Assert.Equal(second.Id, embedding.ChunkId);
                Assert.Equal([0f, 1f], embedding.Embedding);
            });

        embeddingService.Verify(
            x => x.GenerateEmbeddingAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("nan")]
    [InlineData("infinity")]
    [InlineData("zero")]
    public async Task GenerateAsync_RejectsInvalidVectors(
        string caseName)
    {
        float[] vector = caseName switch
        {
            "empty" => [],
            "nan" => [float.NaN, 1f],
            "infinity" => [float.PositiveInfinity, 1f],
            "zero" => [0f, 0f],
            _ => throw new ArgumentOutOfRangeException(
                nameof(caseName))
        };

        var embeddingService = new Mock<IEmbeddingService>();

        embeddingService
            .Setup(x => x.GenerateEmbeddingAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(vector);

        var generator = new ChunkEmbeddingGenerator(
            embeddingService.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => generator.GenerateAsync(
                [CreateChunk("Contenu")]));
    }

    [Fact]
    public async Task GenerateAsync_RejectsDimensionMismatch()
    {
        var embeddingService = new Mock<IEmbeddingService>();

        embeddingService
            .Setup(x => x.GenerateEmbeddingAsync(
                "Premier",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([1f, 0f]);

        embeddingService
            .Setup(x => x.GenerateEmbeddingAsync(
                "Deuxième",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([1f, 0f, 0f]);

        var generator = new ChunkEmbeddingGenerator(
            embeddingService.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => generator.GenerateAsync(
                [
                    CreateChunk("Premier", 0),
                    CreateChunk("Deuxième", 1)
                ]));
    }

    [Fact]
    public async Task GenerateAsync_RespectsCancellation()
    {
        var embeddingService = new Mock<IEmbeddingService>(
            MockBehavior.Strict);

        var generator = new ChunkEmbeddingGenerator(
            embeddingService.Object);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => generator.GenerateAsync(
                [CreateChunk("Contenu")],
                cancellation.Token));

        embeddingService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GenerateAsync_RejectsDuplicateChunks()
    {
        var embeddingService = new Mock<IEmbeddingService>(
            MockBehavior.Strict);

        var generator = new ChunkEmbeddingGenerator(
            embeddingService.Object);

        var chunk = CreateChunk("Contenu");

        await Assert.ThrowsAsync<ArgumentException>(
            () => generator.GenerateAsync([chunk, chunk]));

        embeddingService.VerifyNoOtherCalls();
    }
}
