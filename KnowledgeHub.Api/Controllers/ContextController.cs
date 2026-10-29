using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeHub.Api.Controllers;

[ApiController]
[Route("api/context")]
public sealed class ContextController
    : ControllerBase
{
    private readonly
        IContextBuilderService
        _contextBuilder;

    public ContextController(
        IContextBuilderService
            contextBuilder)
    {
        _contextBuilder =
            contextBuilder;
    }

    [HttpPost]
    public async Task<IActionResult>
        Build(
            AskQuestionRequestDto request,
            CancellationToken cancellationToken)
    {
        var result =
            await _contextBuilder
                .BuildAsync(
                    request.Question,
                    5,
                    cancellationToken);

        return Ok(result);
    }
}