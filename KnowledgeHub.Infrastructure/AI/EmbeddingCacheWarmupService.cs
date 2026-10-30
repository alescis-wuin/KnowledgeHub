using System.Diagnostics;
using KnowledgeHub.Infrastructure.Persistence.Context;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace KnowledgeHub.Infrastructure.AI;

public sealed class EmbeddingCacheWarmupService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly VectorIndexCache _indexCache;

    public EmbeddingCacheWarmupService(
        IServiceProvider serviceProvider,
        VectorIndexCache indexCache)
    {
        _serviceProvider = serviceProvider;
        _indexCache = indexCache;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider
                .GetRequiredService<KnowledgeDbContext>();

            var snapshot = await _indexCache.GetAsync(
                context, cancellationToken);

            stopwatch.Stop();
            Console.WriteLine("====================================");
            Console.WriteLine("KNOWLEDGEHUB VECTOR INDEX WARMUP COMPLETE");
            Console.WriteLine($"Indexed chunks : {snapshot.Count}");
            Console.WriteLine($"Elapsed        : {stopwatch.Elapsed}");
            Console.WriteLine("====================================");
        }
        catch (Exception ex)
        {
            Console.WriteLine("VECTOR INDEX WARMUP FAILED");
            Console.WriteLine(ex);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
