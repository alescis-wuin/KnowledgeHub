using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeHub.Api.Controllers;

[ApiController]
[Route("api/documents/{id:guid}/ensure-index")]
public sealed class DocumentIndexController : ControllerBase
{
    private readonly DocumentIndexRepairService _repair;

    public DocumentIndexController(DocumentIndexRepairService repair)
    {
        _repair = repair;
    }

    [HttpPost]
    public async Task<IActionResult> EnsureIndexed(
        Guid id, CancellationToken cancellationToken)
    {
        var repaired = await _repair.RepairIfIncompleteAsync(
            id, cancellationToken);

        if (!repaired.HasValue)
        {
            return NotFound(ApiResponses.Failure<bool>(
                StatusCodes.Status404NotFound,
                HttpContext.TraceIdentifier,
                "Document introuvable"));
        }

        return Ok(ApiResponses.Success(
            repaired.Value,
            StatusCodes.Status200OK,
            HttpContext.TraceIdentifier));
    }
}
