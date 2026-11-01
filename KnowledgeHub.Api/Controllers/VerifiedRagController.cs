using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Services;
using KnowledgeHub.Application.Telemetry;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeHub.Api.Controllers;

/// <summary>
/// A separate, conservative endpoint; /api/rag/ask stays unchanged.
/// </summary>
[ApiController]
[Route("api/rag")]
public sealed class VerifiedRagController : ControllerBase
{
    private readonly VerifiedRagService _service;

    public VerifiedRagController(VerifiedRagService service)
    {
        _service = service;
    }

    [HttpPost("ask/verified")]
    public async Task<ActionResult<VerifiedRagResponseDto>> AskVerified(
        [FromBody] AskQuestionRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _service.AskAsync(request.Question, cancellationToken);
        VerifiedRagMetrics.Record(result.Verdict);
        return Ok(result);
    }
}
