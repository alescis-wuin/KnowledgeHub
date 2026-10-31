using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeHub.Api.Controllers;

[ApiController]
[Route("api/documents")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _service;

    public DocumentsController(
        IDocumentService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        CancellationToken cancellationToken)
    {
        var documents =
            await _service.GetAllAsync(
                cancellationToken);

        return Ok(
            ApiResponses.Success(
                documents,
                StatusCodes.Status200OK,
                HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var document =
            await _service.GetByIdAsync(
                id,
                cancellationToken);

        if (document is null)
        {
            return NotFound(
                ApiResponses.Failure<DocumentDto>(
                    StatusCodes.Status404NotFound,
                    HttpContext.TraceIdentifier,
                    "Document introuvable"));
        }

        return Ok(
            ApiResponses.Success(
                document,
                StatusCodes.Status200OK,
                HttpContext.TraceIdentifier));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateDocumentDto dto,
        CancellationToken cancellationToken)
    {
        var result =
            await _service.CreateAsync(
                dto,
                cancellationToken);

        var response =
            ApiResponses.Success(
                result,
                StatusCodes.Status201Created,
                HttpContext.TraceIdentifier);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            response);
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import(
        CreateDocumentDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _service.ImportAsync(dto, cancellationToken);

        return Ok(ApiResponses.Success(
            result,
            StatusCodes.Status200OK,
            HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateDocumentDto dto,
        CancellationToken cancellationToken)
    {
        var result =
            await _service.UpdateAsync(
                id,
                dto,
                cancellationToken);

        if (result is null)
        {
            return NotFound(
                ApiResponses.Failure<DocumentDto>(
                    StatusCodes.Status404NotFound,
                    HttpContext.TraceIdentifier,
                    "Document introuvable"));
        }

        return Ok(
            ApiResponses.Success(
                result,
                StatusCodes.Status200OK,
                HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(
            id,
            cancellationToken);

        return Ok(
            ApiResponses.Success(
                "Document supprimé",
                StatusCodes.Status200OK,
                HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:guid}/details")]
    public async Task<IActionResult> GetDetails(
    Guid id,
    CancellationToken cancellationToken)
    {
        var result =
            await _service.GetDetailsAsync(
                id,
                cancellationToken);

        if (result is null)
        {
            return NotFound(
                ApiResponses.Failure<DocumentDetailsDto>(
                    StatusCodes.Status404NotFound,
                    HttpContext.TraceIdentifier,
                    "Document introuvable"));
        }

        return Ok(
            ApiResponses.Success(
                result,
                StatusCodes.Status200OK,
                HttpContext.TraceIdentifier));
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(
    [FromQuery] string q,
    CancellationToken cancellationToken)
    {
        var result =
            await _service.SearchAsync(
                q,
                cancellationToken);

        return Ok(
            ApiResponses.Success(
                result,
                StatusCodes.Status200OK,
                HttpContext.TraceIdentifier));
    }

    [HttpGet("category/{id:guid}")]
    public async Task<IActionResult> GetByCategory(
    Guid id,
    CancellationToken cancellationToken)
    {
        var result =
            await _service.GetByCategoryAsync(
                id,
                cancellationToken);

        return Ok(
            ApiResponses.Success(
                result,
                StatusCodes.Status200OK,
                HttpContext.TraceIdentifier));
    }

    [HttpGet("tag/{id:guid}")]
    public async Task<IActionResult> GetByTag(
    Guid id,
    CancellationToken cancellationToken)
    {
        var result =
            await _service.GetByTagAsync(
                id,
                cancellationToken);

        return Ok(
            ApiResponses.Success(
                result,
                StatusCodes.Status200OK,
                HttpContext.TraceIdentifier));
    }

    [HttpGet("source/{id:guid}")]
    public async Task<IActionResult> GetBySource(
    Guid id,
    CancellationToken cancellationToken)
    {
        var result =
            await _service.GetBySourceAsync(
                id,
                cancellationToken);

        return Ok(
            ApiResponses.Success(
                result,
                StatusCodes.Status200OK,
                HttpContext.TraceIdentifier));
    }
}