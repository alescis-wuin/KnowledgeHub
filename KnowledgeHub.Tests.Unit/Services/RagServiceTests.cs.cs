
using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Infrastructure.AI;
using Moq;
using Xunit;

namespace KnowledgeHub.Tests.Unit.Services;

public sealed class RagServiceTests
{
    [Fact]
    public async Task AskAsync_WithoutSources_DoesNotCallLlm()
    {
        const string question = "Question de test";

        var contextBuilder =
            new Mock<IContextBuilderService>();

        var chatCompletion =
            new Mock<IChatCompletionService>(
                MockBehavior.Strict);

        contextBuilder
            .Setup(x => x.BuildAsync(
                question,
                2,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrievedContextDto
            {
                Question = question,
                Context = string.Empty,
                Sources = []
            });

        var service = new RagService(
            contextBuilder.Object,
            chatCompletion.Object);

        var result = await service.AskAsync(question);

        Assert.Equal(question, result.Question);
        Assert.NotEmpty(result.Answer);
        Assert.Empty(result.Sources);

        chatCompletion.Verify(
            x => x.GenerateAnswerAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AskAsync_WithSources_CallsLlmOnce()
    {
        const string question = "Question de test";
        const string expectedAnswer = "Réponse documentée";

        var contextBuilder =
            new Mock<IContextBuilderService>();

        var chatCompletion =
            new Mock<IChatCompletionService>();

        var source = new VectorSearchResultDto
        {
            ChunkId = Guid.NewGuid(),
            DocumentId = Guid.NewGuid(),
            Title = "Document test",
            Content = "Information pertinente",
            Score = 0.9
        };

        contextBuilder
            .Setup(x => x.BuildAsync(
                question,
                2,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrievedContextDto
            {
                Question = question,
                Context = "Information pertinente",
                Sources = [source]
            });

        chatCompletion
            .Setup(x => x.GenerateAnswerAsync(
                question,
                "Information pertinente",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAnswer);

        var service = new RagService(
            contextBuilder.Object,
            chatCompletion.Object);

        var result = await service.AskAsync(question);

        Assert.Equal(expectedAnswer, result.Answer);
        Assert.Single(result.Sources);

        chatCompletion.Verify(
            x => x.GenerateAnswerAsync(
                question,
                "Information pertinente",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
