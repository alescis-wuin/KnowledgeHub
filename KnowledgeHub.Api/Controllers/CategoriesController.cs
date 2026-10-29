using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeHub.Api.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController
    : ControllerBase
{
    private readonly ICategoryService _service;

    public CategoriesController(
        ICategoryService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        CancellationToken cancellationToken)
    {
        var result =
            await _service.GetAllAsync(
                cancellationToken);

        return Ok(
            ApiResponses.Success(
                result,
                StatusCodes.Status200OK,
                HttpContext.TraceIdentifier));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateCategoryDto dto,
        CancellationToken cancellationToken)
    {
        var result =
            await _service.CreateAsync(
                dto,
                cancellationToken);

        return Ok(
            ApiResponses.Success(
                result,
                StatusCodes.Status201Created,
                HttpContext.TraceIdentifier));
    }
}