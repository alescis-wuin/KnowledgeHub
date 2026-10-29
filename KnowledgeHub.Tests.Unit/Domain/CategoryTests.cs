using FluentAssertions;
using KnowledgeHub.Domain.Entities;

namespace KnowledgeHub.Tests.Unit.Domain;

public class CategoryTests
{
    [Fact]
    public void Constructor_Should_Create_Category()
    {
        var category =
            new Category("Architecture");

        category.Name.Should()
            .Be("Architecture");
    }

    [Fact]
    public void Constructor_Should_Trim_Name()
    {
        var category =
            new Category("  Architecture  ");

        category.Name.Should()
            .Be("Architecture");
    }

    [Fact]
    public void Constructor_Should_Throw_When_Name_Is_Empty()
    {
        var action =
            () => new Category("");

        action.Should()
            .Throw<ArgumentException>();
    }
}