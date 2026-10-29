using System.Diagnostics;
using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace KnowledgeHub.Infrastructure.AI;

public sealed class EmbeddingCacheWarmupService
    : IHostedService
{
    private readonly IServiceProvider
        _serviceProvider;

    private readonly IMemoryCache
        _memoryCache;

    public const string
        EmbeddingsCacheKey =
            "knowledgehub_embeddings";

    public const string
        ChunksCacheKey =
            "knowledgehub_chunks";

    public EmbeddingCacheWarmupService(
        IServiceProvider serviceProvider,
        IMemoryCache memoryCache)
    {
        _serviceProvider =
            serviceProvider;

        _memoryCache =
            memoryCache;
    }

    public async Task StartAsync(
        CancellationToken cancellationToken)
    {
        var stopwatch =
            Stopwatch.StartNew();

        try
        {
            using var scope =
                _serviceProvider.CreateScope();

            var context =
                scope.ServiceProvider
                    .GetRequiredService<
                        KnowledgeDbContext>();

            var embeddings =
                await context
                    .ChunkEmbeddings
                    .AsNoTracking()
                    .ToListAsync(
                        cancellationToken);

            _memoryCache.Set(
                EmbeddingsCacheKey,
                embeddings,
                TimeSpan.FromHours(12));

            var chunks =
                await context
                    .DocumentChunks
                    .AsNoTracking()
                    .Include(
                        x => x.Document)
                    .ToListAsync(
                        cancellationToken);

            var chunkDictionary =
                chunks.ToDictionary(
                    x => x.Id);

            _memoryCache.Set(
                ChunksCacheKey,
                chunkDictionary,
                TimeSpan.FromHours(12));

            stopwatch.Stop();

            Console.WriteLine(
                "====================================");

            Console.WriteLine(
                "KNOWLEDGEHUB CACHE WARMUP COMPLETE");

            Console.WriteLine(
                $"Embeddings : {embeddings.Count}");

            Console.WriteLine(
                $"Chunks     : {chunks.Count}");

            Console.WriteLine(
                $"Elapsed    : {stopwatch.Elapsed}");

            Console.WriteLine(
                "====================================");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "CACHE WARMUP FAILED");

            Console.WriteLine(
                ex);
        }
    }

    public Task StopAsync(
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
