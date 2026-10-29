using FluentValidation;
using KnowledgeHub.Application.DTOs;

namespace KnowledgeHub.Application.Validators;

public class UpdateDocumentDtoValidator
    : AbstractValidator<UpdateDocumentDto>
{
    public UpdateDocumentDtoValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(300);

        RuleFor(x => x.Content)
            .NotEmpty();
    }
}