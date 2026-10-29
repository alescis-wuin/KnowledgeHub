using FluentAssertions;
using KnowledgeHub.Domain.Entities;

namespace KnowledgeHub.Tests.Unit.Domain;

public class DocumentChunkTests
{
    [Fact]
    public void Constructor_Should_Create_Chunk()
    {
        var documentId =
            Guid.NewGuid();

        var chunk =
            new DocumentChunk(
                documentId,
                0,
                5,
                "Mon chunk");

        chunk.DocumentId.Should()
            .Be(documentId);

        chunk.Content.Should()
            .Be("Mon chunk");
    }

    [Fact]
    public void Constructor_Should_Set_ChunkIndex()
    {
        var chunk =
            new DocumentChunk(
                Guid.NewGuid(),
                3,
                5,
                "Contenu");

        chunk.ChunkIndex.Should()
            .Be(3);
    }

    [Fact]
    public void Constructor_Should_Set_TokenCount()
    {
        var chunk =
            new DocumentChunk(
                Guid.NewGuid(),
                1,
                42,
                "Contenu");

        chunk.TokenCount.Should()
            .Be(42);
    }

    [Fact]
    public void Constructor_Should_Trim_Content()
    {
        var chunk =
            new DocumentChunk(
                Guid.NewGuid(),
                0,
                5,
                "  Mon chunk  ");

        chunk.Content.Should()
            .Be("Mon chunk");
    }

    [Fact]
    public void Constructor_Should_Set_CreatedAt()
    {
        var chunk =
            new DocumentChunk(
                Guid.NewGuid(),
                0,
                5,
                "Contenu");

        chunk.CreatedAt.Should()
            .NotBe(default);
    }

    [Fact]
    public void Constructor_Should_Throw_When_Content_Is_Empty()
    {
        var action =
            () => new DocumentChunk(
                Guid.NewGuid(),
                0,
                0,
                "");

        action.Should()
            .Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_Should_Throw_When_Content_Is_Whitespace()
    {
        var action =
            () => new DocumentChunk(
                Guid.NewGuid(),
                0,
                0,
                "     ");

        action.Should()
            .Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_Should_Throw_When_ChunkIndex_Is_Negative()
    {
        var action =
            () => new DocumentChunk(
                Guid.NewGuid(),
                -1,
                10,
                "Contenu");

        action.Should()
            .Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_Should_Throw_When_TokenCount_Is_Negative()
    {
        var action =
            () => new DocumentChunk(
                Guid.NewGuid(),
                0,
                -1,
                "Contenu");

        action.Should()
            .Throw<ArgumentException>();
    }
}