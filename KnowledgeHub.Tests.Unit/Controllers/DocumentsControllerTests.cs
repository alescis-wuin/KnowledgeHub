using FluentAssertions;
using KnowledgeHub.Api.Controllers;
using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace KnowledgeHub.Tests.Unit.Controllers;

public class DocumentsControllerTests
{
    private readonly Mock<IDocumentService> _serviceMock;

    private readonly DocumentsController _controller;

    public DocumentsControllerTests()
    {
        _serviceMock =
            new Mock<IDocumentService>();

        _controller =
            new DocumentsController(
                _serviceMock.Object);

        _controller.ControllerContext =
            new ControllerContext
            {
                HttpContext =
                    new DefaultHttpContext()
            };
    }

    [Fact]
    public async Task Get_Should_Return_Ok()
    {
        _serviceMock
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new List<DocumentDto>());

        var result =
            await _controller.Get(
                CancellationToken.None);

        result.Should()
            .BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Get_Should_Return_ApiResponse()
    {
        var documents =
            new List<DocumentDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Title = "Test"
                }
            };

        _serviceMock
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(documents);

        var result =
            await _controller.Get(
                CancellationToken.None);

        var ok =
            result as OkObjectResult;

        ok.Should().NotBeNull();

        var response =
            ok!.Value as ApiResponse<List<DocumentDto>>;

        response.Should().NotBeNull();

        response!.Success.Should().BeTrue();

        response.StatusCode.Should().Be(
            StatusCodes.Status200OK);

        response.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetById_Should_Return_Ok()
    {
        var document =
            new DocumentDto
            {
                Id = Guid.NewGuid(),
                Title = "Test"
            };

        _serviceMock
            .Setup(x => x.GetByIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result =
            await _controller.GetById(
                document.Id,
                CancellationToken.None);

        var ok =
            result as OkObjectResult;

        ok.Should().NotBeNull();

        var response =
            ok!.Value as ApiResponse<DocumentDto>;

        response.Should().NotBeNull();

        response!.Success.Should().BeTrue();

        response.Data.Should().NotBeNull();

        response.Data!.Id.Should().Be(
            document.Id);
    }

    [Fact]
    public async Task GetById_Should_Return_NotFound()
    {
        _serviceMock
            .Setup(x => x.GetByIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (DocumentDto?)null);

        var result =
            await _controller.GetById(
                Guid.NewGuid(),
                CancellationToken.None);

        var notFound =
            result as NotFoundObjectResult;

        notFound.Should().NotBeNull();

        var response =
            notFound!.Value as ApiResponse<DocumentDto>;

        response.Should().NotBeNull();

        response!.Success.Should().BeFalse();

        response.StatusCode.Should().Be(
            StatusCodes.Status404NotFound);

        response.Errors.Should().Contain(
            "Document introuvable");
    }

    [Fact]
    public async Task Create_Should_Return_CreatedAtAction()
    {
        var dto =
            new CreateDocumentDto
            {
                Title = "Titre",
                Content = "Contenu"
            };

        var document =
            new DocumentDto
            {
                Id = Guid.NewGuid(),
                Title = dto.Title,
                Content = dto.Content
            };

        _serviceMock
            .Setup(x => x.CreateAsync(
                It.IsAny<CreateDocumentDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result =
            await _controller.Create(
                dto,
                CancellationToken.None);

        var created =
            result as CreatedAtActionResult;

        created.Should().NotBeNull();

        created!.ActionName.Should().Be(
            nameof(DocumentsController.GetById));

        var response =
            created.Value as ApiResponse<DocumentDto>;

        response.Should().NotBeNull();

        response!.Success.Should().BeTrue();

        response.StatusCode.Should().Be(
            StatusCodes.Status201Created);

        response.Data!.Id.Should().Be(
            document.Id);
    }

    [Fact]
    public async Task Update_Should_Return_Ok()
    {
        var updated =
            new DocumentDto
            {
                Id = Guid.NewGuid(),
                Title = "Updated"
            };

        _serviceMock
            .Setup(x => x.UpdateAsync(
                It.IsAny<Guid>(),
                It.IsAny<UpdateDocumentDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(updated);

        var result =
            await _controller.Update(
                updated.Id,
                new UpdateDocumentDto
                {
                    Title = "Updated",
                    Content = "Content"
                },
                CancellationToken.None);

        var ok =
            result as OkObjectResult;

        ok.Should().NotBeNull();

        var response =
            ok!.Value as ApiResponse<DocumentDto>;

        response.Should().NotBeNull();

        response!.Success.Should().BeTrue();

        response.Data!.Id.Should().Be(
            updated.Id);
    }

    [Fact]
    public async Task Update_Should_Return_NotFound()
    {
        _serviceMock
            .Setup(x => x.UpdateAsync(
                It.IsAny<Guid>(),
                It.IsAny<UpdateDocumentDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (DocumentDto?)null);

        var result =
            await _controller.Update(
                Guid.NewGuid(),
                new UpdateDocumentDto(),
                CancellationToken.None);

        var notFound =
            result as NotFoundObjectResult;

        notFound.Should().NotBeNull();

        var response =
            notFound!.Value as ApiResponse<DocumentDto>;

        response.Should().NotBeNull();

        response!.Success.Should().BeFalse();

        response.StatusCode.Should().Be(
            StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Delete_Should_Return_Ok()
    {
        var result =
            await _controller.Delete(
                Guid.NewGuid(),
                CancellationToken.None);

        var ok =
            result as OkObjectResult;

        ok.Should().NotBeNull();

        var response =
            ok!.Value as ApiResponse<string>;

        response.Should().NotBeNull();

        response!.Success.Should().BeTrue();

        response.Data.Should().Be(
            "Document supprimé");
    }

    [Fact]
    public async Task Create_Should_Call_Service_Once()
    {
        var dto =
            new CreateDocumentDto
            {
                Title = "Titre",
                Content = "Contenu"
            };

        _serviceMock
            .Setup(x => x.CreateAsync(
                It.IsAny<CreateDocumentDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new DocumentDto
                {
                    Id = Guid.NewGuid()
                });

        await _controller.Create(
            dto,
            CancellationToken.None);

        _serviceMock.Verify(
            x => x.CreateAsync(
                It.IsAny<CreateDocumentDto>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Delete_Should_Call_Service_Once()
    {
        var id = Guid.NewGuid();

        await _controller.Delete(
            id,
            CancellationToken.None);

        _serviceMock.Verify(
            x => x.DeleteAsync(
                id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}