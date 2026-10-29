using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Infrastructure.AI;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeHub.Api.Controllers;

[ApiController]
[Route("api/rag")]
public sealed class RagController
    : ControllerBase
{
    private readonly RagService
        _ragService;

    public RagController(
        RagService ragService)
    {
        _ragService =
            ragService;
    }

    [HttpPost("ask")]
    public async Task<IActionResult>
        Ask(
            AskQuestionRequestDto request,
            CancellationToken cancellationToken)
    {
        var result =
            await _ragService
                .AskAsync(
                    request.Question,
                    cancellationToken);

        return Ok(result);
    }
}