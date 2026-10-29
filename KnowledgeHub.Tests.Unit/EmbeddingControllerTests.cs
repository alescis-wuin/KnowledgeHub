
using FluentAssertions;
using KnowledgeHub.Api.Controllers;
using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace KnowledgeHub.Tests.Unit;

public class EmbeddingControllerTests
{
    [Fact]
    public async Task Should_Return_Embedding()
    {
        var service = new Mock<IEmbeddingService>();

        service
            .Setup(x => x.GenerateEmbeddingAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f, 0.3f]);

        var controller = new EmbeddingsController(
            service.Object);

        var response = await controller.Generate(
            new EmbeddingRequestDto
            {
                Text = "test"
            },
            default);

        response.Should()
            .BeOfType<OkObjectResult>();
    }
}
