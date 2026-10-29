using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeHub.Api.Controllers;

[ApiController]
[Route("api/tags")]
public class TagsController : ControllerBase
{
    private readonly ITagService _service;

    public TagsController(
        ITagService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        CancellationToken cancellationToken)
    {
        var tags =
            await _service.GetAllAsync(
                cancellationToken);

        return Ok(
            ApiResponses.Success(
                tags,
                StatusCodes.Status200OK,
                HttpContext.TraceIdentifier));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateTagDto dto,
        CancellationToken cancellationToken)
    {
        var tag =
            await _service.CreateAsync(
                dto,
                cancellationToken);

        return Ok(
            ApiResponses.Success(
                tag,
                StatusCodes.Status201Created,
                HttpContext.TraceIdentifier));
    }
}