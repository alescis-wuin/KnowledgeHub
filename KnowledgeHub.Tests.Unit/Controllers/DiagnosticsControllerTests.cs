using FluentAssertions;
using KnowledgeHub.Api.Controllers;
using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace KnowledgeHub.Tests.Unit.Controllers;

public class DiagnosticsControllerTests
{
    [Fact]
    public async Task Database_Should_Return_Ok()
    {
        var options =
            new DbContextOptionsBuilder<KnowledgeDbContext>()
                .UseInMemoryDatabase(
                    Guid.NewGuid().ToString())
                .Options;

        await using var context =
            new KnowledgeDbContext(options);

        var controller =
            new DiagnosticsController(context);

        controller.ControllerContext =
            new ControllerContext
            {
                HttpContext =
                    new DefaultHttpContext()
            };

        var result =
            await controller.Database();

        var ok =
            result as OkObjectResult;

        ok.Should().NotBeNull();

        var response =
            ok!.Value as ApiResponse<DatabaseStatusDto>;

        response.Should().NotBeNull();

        response!.Success.Should().BeTrue();

        response.Data!.Connected.Should().BeTrue();
    }
}