using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeHub.Api.Controllers;

[ApiController]
[Route("api/vector")]
public sealed class VectorSearchController
    : ControllerBase
{
    private readonly IVectorSearchService
        _vectorSearchService;

    public VectorSearchController(
        IVectorSearchService vectorSearchService)
    {
        _vectorSearchService =
            vectorSearchService;
    }

    [HttpPost("search")]
    public async Task<IActionResult>
        Search(
            VectorSearchRequestDto request,
            CancellationToken cancellationToken)
    {
        var results =
            await _vectorSearchService
                .SearchAsync(
                    request.Question,
                    request.TopK,
                    cancellationToken);

        return Ok(results);
    }
}