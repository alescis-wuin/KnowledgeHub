using FluentAssertions;
using KnowledgeHub.Domain.Entities;

namespace KnowledgeHub.Tests.Unit.Domain;

public class TagTests
{
    [Fact]
    public void Constructor_Should_Create_Tag()
    {
        var tag =
            new Tag("dotnet");

        tag.Name.Should()
            .Be("dotnet");
    }

    [Fact]
    public void Constructor_Should_Trim_Name()
    {
        var tag =
            new Tag("  dotnet  ");

        tag.Name.Should()
            .Be("dotnet");
    }

    [Fact]
    public void DocumentTags_Should_Be_Initialized()
    {
        var tag =
            new Tag("dotnet");

        tag.DocumentTags.Should()
            .NotBeNull();

        tag.DocumentTags.Should()
            .BeEmpty();
    }
}
