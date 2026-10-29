using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeHub.Api.Controllers;

[ApiController]
[Route("api/embeddings")]
public class EmbeddingsController
    : ControllerBase
{
    private readonly IEmbeddingService
        _embeddingService;

    public EmbeddingsController(
        IEmbeddingService embeddingService)
    {
        _embeddingService =
            embeddingService;
    }

    [HttpPost]
    public async Task<IActionResult> Generate(
        EmbeddingRequestDto request,
        CancellationToken cancellationToken)
    {
        var embedding =
            await _embeddingService
                .GenerateEmbeddingAsync(
                    request.Text,
                    cancellationToken);

        return Ok(
            new EmbeddingResponseDto
            {
                Dimensions =
                    embedding.Length,

                Embedding =
                    embedding
            });
    }
}