using KnowledgeHub.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace KnowledgeHub.Tests.Integration;

public sealed class IsolatedPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("knowledgehub_tests")
            .WithUsername("kh_test")
            .WithPassword("kh_test_password")
            .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync()
    {
        return _container.DisposeAsync().AsTask();
    }

    public KnowledgeDbContext CreateContext()
    {
        var options =
            new DbContextOptionsBuilder<KnowledgeDbContext>()
                .UseNpgsql(ConnectionString)
                .Options;

        return new KnowledgeDbContext(options);
    }
}
