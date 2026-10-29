using FluentAssertions;
using KnowledgeHub.Domain.Entities;

namespace KnowledgeHub.Tests.Unit.Domain;

public class KnowledgeSourceTests
{
    [Fact]
    public void Constructor_Should_Create_Source()
    {
        var source =
            new KnowledgeSource(
                "Confluence",
                "https://confluence.local");

        source.Name.Should()
            .Be("Confluence");

        source.Url.Should()
            .Be("https://confluence.local");
    }

    [Fact]
    public void Constructor_Should_Allow_Null_Url()
    {
        var source =
            new KnowledgeSource(
                "Utilisateur");

        source.Url.Should()
            .BeNull();
    }
}