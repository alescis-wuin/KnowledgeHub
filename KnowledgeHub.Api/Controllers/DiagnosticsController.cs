using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeHub.Api.Controllers;

[ApiController]
[Route("api/diagnostics")]
public class DiagnosticsController : ControllerBase
{
    private readonly KnowledgeDbContext _context;

    public DiagnosticsController(
        KnowledgeDbContext context)
    {
        _context = context;
    }

    [HttpGet("database")]
    public async Task<IActionResult> Database()
    {
        var connected =
            await _context.Database.CanConnectAsync();

        return Ok(
            ApiResponses.Success(
                new DatabaseStatusDto(
                    connected,
                    DateTime.UtcNow),
                StatusCodes.Status200OK,
                HttpContext.TraceIdentifier));
    }
}