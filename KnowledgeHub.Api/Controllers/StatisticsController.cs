using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/statistics")]
public class StatisticsController
    : ControllerBase
{
    private readonly IDocumentService _service;

    public StatisticsController(
        IDocumentService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        CancellationToken cancellationToken)
    {
        var result =
            await _service.GetStatisticsAsync(
                cancellationToken);

        return Ok(
            ApiResponses.Success(
                result,
                StatusCodes.Status200OK,
                HttpContext.TraceIdentifier));
    }
}