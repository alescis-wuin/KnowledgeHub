using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeHub.Api.Controllers;

[ApiController]
[Route("api/sources")]
public class SourcesController
    : ControllerBase
{
    private readonly IKnowledgeSourceService _service;

    public SourcesController(
        IKnowledgeSourceService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        CancellationToken cancellationToken)
    {
        var sources =
            await _service.GetAllAsync(
                cancellationToken);

        return Ok(
            ApiResponses.Success(
                sources,
                StatusCodes.Status200OK,
                HttpContext.TraceIdentifier));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateKnowledgeSourceDto dto,
        CancellationToken cancellationToken)
    {
        var source =
            await _service.CreateAsync(
                dto,
                cancellationToken);

        return Ok(
            ApiResponses.Success(
                source,
                StatusCodes.Status201Created,
                HttpContext.TraceIdentifier));
    }
}