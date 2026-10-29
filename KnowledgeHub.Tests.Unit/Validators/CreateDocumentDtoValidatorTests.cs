using FluentAssertions;
using FluentValidation;
using FluentValidation.TestHelper;
using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Validators;

namespace KnowledgeHub.Tests.Unit.Validators;

public class CreateDocumentDtoValidatorTests
{
    private readonly CreateDocumentDtoValidator _validator =
        new();

    [Fact]
    public void Should_Fail_When_Title_Is_Empty()
    {
        var dto =
            new CreateDocumentDto
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
        var dto = new CreateDocumentDto
        {
            Title = "Titre",
            Content = ""
        };

        var result =
            _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(
            x => x.Content);
    }

    [Fact]
    public void Should_Pass_When_Model_Is_Valid()
    {
        var dto = new CreateDocumentDto
        {
            Title = "Titre",
            Content = "Contenu"
        };

        var result =
            _validator.TestValidate(dto);

        result.ShouldNotHaveAnyValidationErrors();
    }
}