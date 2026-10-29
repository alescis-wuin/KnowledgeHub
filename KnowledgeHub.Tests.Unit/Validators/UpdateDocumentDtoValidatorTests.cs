using FluentAssertions;
using FluentValidation.TestHelper;
using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Validators;

namespace KnowledgeHub.Tests.Unit.Validators;

public class UpdateDocumentDtoValidatorTests
{
    private readonly UpdateDocumentDtoValidator _validator =
        new();

    [Fact]
    public void Should_Fail_When_Title_Is_Empty()
    {
        var dto =
            new UpdateDocumentDto
            {
                Title = "",
                Content = "Contenu"
            };

        var result =
            _validator.Validate(dto);

        result.Errors
            .Should()
            .Contain(x =>
                x.PropertyName == nameof(UpdateDocumentDto.Title));
        result.IsValid
            .Should()
            .BeFalse();
    }

    [Fact]
    public void Should_Fail_When_Content_Is_Empty()
    {
        var dto = new UpdateDocumentDto
        {
            Title = "Titre",
            Content = string.Empty
        };

        var result =
            _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(
            x => x.Content);
    }

    [Fact]
    public void Should_Fail_When_Title_Too_Long()
    {
        var dto = new UpdateDocumentDto
        {
            Title = new string('A', 301),
            Content = "Contenu"
        };

        var result =
            _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(
            x => x.Title);
    }

    [Fact]
    public void Should_Pass_When_Model_Is_Valid()
    {
        var dto = new UpdateDocumentDto
        {
            Title = "Titre valide",
            Content = "Contenu valide"
        };

        var result =
            _validator.TestValidate(dto);

        result.ShouldNotHaveAnyValidationErrors();
    }
}