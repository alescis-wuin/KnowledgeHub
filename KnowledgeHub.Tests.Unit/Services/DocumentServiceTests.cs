using FluentAssertions;
using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Application.Services;
using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Domain.Interfaces;
using Moq;

namespace KnowledgeHub.Tests.Unit.Services;

public class DocumentServiceTests
{
    private readonly Mock<IKnowledgeDocumentRepository>
        _repositoryMock;

    private readonly Mock<ICategoryRepository>
        _categoryRepositoryMock;

    private readonly Mock<IKnowledgeSourceRepository>
        _sourceRepositoryMock;

    private readonly Mock<ITagRepository>
        _tagRepositoryMock;

    private readonly Mock<ITextChunker>
        _chunkerMock;

	private readonly Mock<IChunkEmbeddingGenerator>
	    _embeddingGeneratorMock;

    private readonly DocumentService _service;

    public DocumentServiceTests()
    {
        _repositoryMock =
            new Mock<IKnowledgeDocumentRepository>();

        _categoryRepositoryMock =
            new Mock<ICategoryRepository>();

        _sourceRepositoryMock =
            new Mock<IKnowledgeSourceRepository>();

        _tagRepositoryMock =
            new Mock<ITagRepository>();

        _chunkerMock =
            new Mock<ITextChunker>();

        _chunkerMock
            .Setup(x => x.Chunk(
                It.IsAny<string>()))
            .Returns(
                new List<string>
                {
                    "chunk-1"
                });

		_embeddingGeneratorMock = new Mock<IChunkEmbeddingGenerator>();

		_embeddingGeneratorMock
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

        _service =
            new DocumentService(
                _repositoryMock.Object,
                _categoryRepositoryMock.Object,
                _sourceRepositoryMock.Object,
                _tagRepositoryMock.Object,
                _chunkerMock.Object,
                _embeddingGeneratorMock.Object);
    }

    [Fact]
    public async Task Should_Create_Document()
    {
        var dto =
            new CreateDocumentDto
            {
                Title = "Titre",
                Content = "Contenu"
            };

        var result =
            await _service.CreateAsync(dto);

        result.Should().NotBeNull();

        result.Title.Should().Be("Titre");

        result.Content.Should().Be("Contenu");

		_repositoryMock.Verify(
		    x => x.AddIndexedAsync(
		        It.IsAny<KnowledgeDocument>(),
		        It.IsAny<IReadOnlyCollection<ChunkEmbedding>>(),
		        It.IsAny<CancellationToken>()),
		    Times.Once);
    }

    [Fact]
    public async Task Should_Generate_Chunks_When_Creating_Document()
    {
        var dto =
            new CreateDocumentDto
            {
                Title = "Titre",
                Content = "Contenu très long"
            };

        _chunkerMock
            .Setup(x => x.Chunk(
                dto.Content))
            .Returns(
                new List<string>
                {
                    "chunk1",
                    "chunk2"
                });

        await _service.CreateAsync(dto);

        _chunkerMock.Verify(
            x => x.Chunk(
                dto.Content),
            Times.Once);
    }

    [Fact]
    public async Task Should_Return_All_Documents()
    {
        var documents =
            new List<KnowledgeDocument>
            {
                new(
                    "Doc1",
                    "Contenu1"),

                new(
                    "Doc2",
                    "Contenu2")
            };

        _repositoryMock
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(documents);

        var result =
            await _service.GetAllAsync();

        result.Should()
            .HaveCount(2);
    }

    [Fact]
    public async Task Should_Return_Document_By_Id()
    {
        var document =
            new KnowledgeDocument(
                "Titre",
                "Contenu");

        _repositoryMock
            .Setup(x => x.GetByIdAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result =
            await _service.GetByIdAsync(
                document.Id);

        result.Should()
            .NotBeNull();

        result!.Title.Should()
            .Be("Titre");
    }

    [Fact]
    public async Task Should_Return_Null_When_Not_Found()
    {
        _repositoryMock
            .Setup(x => x.GetByIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (KnowledgeDocument?)null);

        var result =
            await _service.GetByIdAsync(
                Guid.NewGuid());

        result.Should()
            .BeNull();
    }

    [Fact]
    public async Task Should_Update_Document()
    {
        var document =
            new KnowledgeDocument(
                "Ancien titre",
                "Ancien contenu");

        _repositoryMock
            .Setup(x => x.GetByIdAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var dto =
            new UpdateDocumentDto
            {
                Title = "Nouveau titre",
                Content = "Nouveau contenu"
            };

        var result =
            await _service.UpdateAsync(
                document.Id,
                dto);

        result.Should()
            .NotBeNull();

        result!.Title.Should()
            .Be("Nouveau titre");

		_repositoryMock.Verify(
		    x => x.UpdateIndexedAsync(
		        It.IsAny<KnowledgeDocument>(),
		        It.IsAny<IReadOnlyCollection<ChunkEmbedding>>(),
		        It.IsAny<CancellationToken>()),
		    Times.Once);
    }

    [Fact]
    public async Task Should_Return_Null_When_Update_Target_Not_Found()
    {
        _repositoryMock
            .Setup(x => x.GetByIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (KnowledgeDocument?)null);

        var result =
            await _service.UpdateAsync(
                Guid.NewGuid(),
                new UpdateDocumentDto
                {
                    Title = "Test",
                    Content = "Test"
                });

        result.Should()
            .BeNull();
    }

    [Fact]
    public async Task Should_Delete_Document()
    {
        var id =
            Guid.NewGuid();

        await _service.DeleteAsync(id);

        _repositoryMock.Verify(
            x => x.DeleteAsync(
                id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Should_Propagate_CancellationToken_On_GetById()
    {
        var token =
            new CancellationTokenSource().Token;

        var document =
            new KnowledgeDocument(
                "Titre",
                "Contenu");

        _repositoryMock
            .Setup(x => x.GetByIdAsync(
                It.IsAny<Guid>(),
                token))
            .ReturnsAsync(document);

        await _service.GetByIdAsync(
            document.Id,
            token);

        _repositoryMock.Verify(
            x => x.GetByIdAsync(
                document.Id,
                token),
            Times.Once);
    }

    [Fact]
    public async Task Should_Call_Repository_Only_Once_On_GetById()
    {
        var document =
            new KnowledgeDocument(
                "Titre",
                "Contenu");

        _repositoryMock
            .Setup(x => x.GetByIdAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        await _service.GetByIdAsync(
            document.Id);

        _repositoryMock.Verify(
            x => x.GetByIdAsync(
                document.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Should_Return_Details_When_Document_Exists()
    {
        var document =
            new KnowledgeDocument(
                "Titre",
                "Contenu");

        _repositoryMock
            .Setup(x => x.GetByIdAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result =
            await _service.GetDetailsAsync(
                document.Id);

        result.Should()
            .NotBeNull();

        result!.Title.Should()
            .Be("Titre");
    }

    [Fact]
    public async Task Should_Return_Null_Details_When_Document_Not_Found()
    {
        _repositoryMock
            .Setup(x => x.GetByIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (KnowledgeDocument?)null);

        var result =
            await _service.GetDetailsAsync(
                Guid.NewGuid());

        result.Should()
            .BeNull();
    }

    [Fact]
    public async Task Should_Search_Documents()
    {
        var documents =
            new List<KnowledgeDocument>
            {
            new(
                "PostgreSQL",
                "Base de données")
            };

        _repositoryMock
            .Setup(x => x.SearchAsync(
                "PostgreSQL",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(documents);

        var result =
            await _service.SearchAsync(
                "PostgreSQL");

        result.Should()
            .HaveCount(1);

        result.First()
            .Title.Should()
            .Be("PostgreSQL");
    }

    [Fact]
    public async Task Should_Return_Documents_By_Category()
    {
        var categoryId =
            Guid.NewGuid();

        var documents =
            new List<KnowledgeDocument>
            {
            new(
                "Architecture",
                "Contenu")
            };

        _repositoryMock
            .Setup(x => x.GetByCategoryAsync(
                categoryId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(documents);

        var result =
            await _service.GetByCategoryAsync(
                categoryId);

        result.Should()
            .HaveCount(1);
    }

    [Fact]
    public async Task Should_Return_Documents_By_Source()
    {
        var sourceId =
            Guid.NewGuid();

        var documents =
            new List<KnowledgeDocument>
            {
            new(
                "Confluence",
                "Contenu")
            };

        _repositoryMock
            .Setup(x => x.GetBySourceAsync(
                sourceId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(documents);

        var result =
            await _service.GetBySourceAsync(
                sourceId);

        result.Should()
            .HaveCount(1);
    }

    [Fact]
    public async Task Should_Return_Documents_By_Tag()
    {
        var tagId =
            Guid.NewGuid();

        var documents =
            new List<KnowledgeDocument>
            {
            new(
                "EF Core",
                "Contenu")
            };

        _repositoryMock
            .Setup(x => x.GetByTagAsync(
                tagId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(documents);

        var result =
            await _service.GetByTagAsync(
                tagId);

        result.Should()
            .HaveCount(1);
    }
}
