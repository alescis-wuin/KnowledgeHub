using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Infrastructure.AI;
using KnowledgeHub.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;

namespace KnowledgeHub.Tests.Integration;

public sealed class CustomWebApplicationFactory
    : WebApplicationFactory<global::Program>, IAsyncLifetime
{
    private readonly IsolatedPostgresFixture _database = new();

    public Task InitializeAsync()
    {
        return _database.InitializeAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        try
        {
            Dispose();
        }
        finally
        {
            await _database.DisposeAsync();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Defense in depth: override both application configuration and EF DI.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:KnowledgeHubDatabase"] =
                    _database.ConnectionString
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<
                IDbContextOptionsConfiguration<KnowledgeDbContext>>();
            services.RemoveAll<DbContextOptions<KnowledgeDbContext>>();
            services.RemoveAll<KnowledgeDbContext>();

            services.AddDbContext<KnowledgeDbContext>(options =>
                options.UseNpgsql(_database.ConnectionString));

            // Exercise the actual ChunkEmbeddingGenerator without contacting Ollama.
            services.RemoveAll<IEmbeddingService>();
            services.AddScoped<
                IEmbeddingService,
                DeterministicEmbeddingService>();

            // The startup cache is unrelated to document CRUD tests.
            var warmupDescriptors = services
                .Where(descriptor =>
                    descriptor.ServiceType == typeof(IHostedService) &&
                    descriptor.ImplementationType ==
                        typeof(EmbeddingCacheWarmupService))
                .ToArray();

            foreach (var descriptor in warmupDescriptors)
            {
                services.Remove(descriptor);
            }
        });
    }

    // Invoked from each HTTP test's IAsyncLifetime.InitializeAsync.
    public async Task ResetDocumentsAsync(
        CancellationToken cancellationToken = default)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<KnowledgeDbContext>();

        // Fail closed: never run a destructive test reset on the dev database.
        var actual = new NpgsqlConnectionStringBuilder(
            context.Database.GetDbConnection().ConnectionString);
        var expected = new NpgsqlConnectionStringBuilder(
            _database.ConnectionString);

        if (!string.Equals(actual.Host, expected.Host,
                StringComparison.OrdinalIgnoreCase) ||
            actual.Port != expected.Port ||
            !string.Equals(actual.Database, expected.Database,
                StringComparison.Ordinal) ||
            !string.Equals(actual.Username, expected.Username,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "HTTP test host is not using its isolated PostgreSQL container.");
        }

        await context.Documents.ExecuteDeleteAsync(cancellationToken);
    }
}
