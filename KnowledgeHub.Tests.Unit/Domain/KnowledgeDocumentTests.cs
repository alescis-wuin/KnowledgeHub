using FluentAssertions;
using KnowledgeHub.Domain.Entities;

namespace KnowledgeHub.Tests.Unit.Domain;

public class KnowledgeDocumentTests
{
    [Fact]
    public void Constructor_Should_Create_Document()
    {
        var document =
            new KnowledgeDocument(
                "Titre",
                "Contenu");

        document.Id.Should()
            .NotBe(Guid.Empty);

        document.Title.Should()
            .Be("Titre");

        document.Content.Should()
            .Be("Contenu");

        document.CreatedAt.Should()
            .NotBe(default);
    }

    [Fact]
    public void Update_Should_Modify_Title_And_Content()
    {
        var document =
            new KnowledgeDocument(
                "Ancien",
                "Ancien contenu");

        document.Update(
            "Nouveau",
            "Nouveau contenu");

        document.Title.Should()
            .Be("Nouveau");

        document.Content.Should()
            .Be("Nouveau contenu");

        document.UpdatedAt.Should()
            .NotBeNull();
    }

    [Fact]
    public void AssignCategory_Should_Update_CategoryId()
    {
        var document =
            new KnowledgeDocument(
                "Titre",
                "Contenu");

        var categoryId = Guid.NewGuid();

        document.AssignCategory(categoryId);

        document.CategoryId.Should()
            .Be(categoryId);
    }

    [Fact]
    public void AssignSource_Should_Update_SourceId()
    {
        var document =
            new KnowledgeDocument(
                "Titre",
                "Contenu");

        var sourceId = Guid.NewGuid();

        document.AssignSource(sourceId);

        document.SourceId.Should()
            .Be(sourceId);
    }

    [Fact]
    public void AddTag_Should_Add_Tag()
    {
        var document =
            new KnowledgeDocument(
                "Titre",
                "Contenu");

        var tag =
            new Tag("dotnet");

        document.AddTag(tag);

        document.DocumentTags.Should()
            .HaveCount(1);

        document.DocumentTags.First()
            .TagId.Should()
            .Be(tag.Id);
    }

    [Fact]
    public void AddTag_Should_Not_Add_Duplicate_Tag()
    {
        var document =
            new KnowledgeDocument(
                "Titre",
                "Contenu");

        var tag =
            new Tag("dotnet");

        document.AddTag(tag);

        document.AddTag(tag);

        document.DocumentTags.Should()
            .HaveCount(1);
    }

    [Fact]
    public void ClearTags_Should_Remove_All_Tags()
    {
        var document =
            new KnowledgeDocument(
                "Titre",
                "Contenu");

        document.AddTag(
            new Tag("dotnet"));

        document.AddTag(
            new Tag("efcore"));

        document.DocumentTags.Should()
            .HaveCount(2);

        document.ClearTags();

        document.DocumentTags.Should()
            .BeEmpty();
    }

    [Fact]
    public void AddChunk_Should_Add_Chunk()
    {
        var document =
            new KnowledgeDocument(
                "Titre",
                "Contenu");

        document.AddChunk(
            0,
            10,
            "Premier chunk");

        document.Chunks.Should()
            .HaveCount(1);

        var chunk =
            document.Chunks.First();

        chunk.Content.Should()
            .Be("Premier chunk");

        chunk.ChunkIndex.Should()
            .Be(0);

        chunk.TokenCount.Should()
            .Be(10);
    }

    [Fact]
    public void ClearChunks_Should_Remove_All_Chunks()
    {
        var document =
            new KnowledgeDocument(
                "Titre",
                "Contenu");

        document.AddChunk(
            0,
            10,
            "Chunk 1");

        document.AddChunk(
            1,
            12,
            "Chunk 2");

        document.Chunks.Should()
            .HaveCount(2);

        document.ClearChunks();

        document.Chunks.Should()
            .BeEmpty();
    }

    [Fact]
    public void Constructor_Should_Throw_When_Title_Is_Empty()
    {
        var action =
            () => new KnowledgeDocument(
                "",
                "Contenu");

        action.Should()
            .Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_Should_Throw_When_Content_Is_Empty()
    {
        var action =
            () => new KnowledgeDocument(
                "Titre",
                "");

        action.Should()
            .Throw<ArgumentException>();
    }

    [Fact]
    public void Update_Should_Throw_When_Title_Is_Empty()
    {
        var document =
            new KnowledgeDocument(
                "Titre",
                "Contenu");

        var action =
            () => document.Update(
                "",
                "Contenu");

        action.Should()
            .Throw<ArgumentException>();
    }

    [Fact]
    public void Update_Should_Throw_When_Content_Is_Empty()
    {
        var document =
            new KnowledgeDocument(
                "Titre",
                "Contenu");

        var action =
            () => document.Update(
                "Titre",
                "");

        action.Should()
            .Throw<ArgumentException>();
    }

    [Fact]
    public void Document_Should_Start_With_Empty_Chunks()
    {
        var document =
            new KnowledgeDocument(
                "Titre",
                "Contenu");

        document.Chunks.Should()
            .BeEmpty();
    }

    [Fact]
    public void Document_Should_Start_With_Empty_DocumentTags()
    {
        var document =
            new KnowledgeDocument(
                "Titre",
                "Contenu");

        document.DocumentTags.Should()
            .BeEmpty();
    }

    [Fact]
    public void Document_Should_Start_Without_Category()
    {
        var document =
            new KnowledgeDocument(
                "Titre",
                "Contenu");

        document.CategoryId.Should()
            .BeNull();
    }

    [Fact]
    public void Document_Should_Start_Without_Source()
    {
        var document =
            new KnowledgeDocument(
                "Titre",
                "Contenu");

        document.SourceId.Should()
            .BeNull();
    }

    [Fact]
    public void SetSummary_Should_Update_Summary()
    {
        var document =
            new KnowledgeDocument(
                "Titre",
                "Contenu");

        document.SetSummary(
            "Résumé");

        document.Summary.Should()
            .Be("Résumé");
    }

    [Fact]
    public void SetSearchKeywords_Should_Update_Keywords()
    {
        var document =
            new KnowledgeDocument(
                "Titre",
                "Contenu");

        document.SetSearchKeywords(
            "dotnet,efcore");

        document.SearchKeywords.Should()
            .Be("dotnet,efcore");
    }
}
