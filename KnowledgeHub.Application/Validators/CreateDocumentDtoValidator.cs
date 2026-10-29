using FluentValidation;
using KnowledgeHub.Application.DTOs;

namespace KnowledgeHub.Application.Validators;

public class CreateDocumentDtoValidator
    : AbstractValidator<CreateDocumentDto>
{
    public CreateDocumentDtoValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(300);

        RuleFor(x => x.Content)
            .NotEmpty();
    }
}