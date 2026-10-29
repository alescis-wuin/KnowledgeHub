using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Domain.Interfaces;
using KnowledgeHub.Infrastructure.AI;
using KnowledgeHub.Infrastructure.Persistence.Context;
using KnowledgeHub.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace KnowledgeHub.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<KnowledgeDbContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString(
                    "KnowledgeHubDatabase"));
        });

        services.AddScoped<
            IKnowledgeDocumentRepository,
            KnowledgeDocumentRepository>();

        services.AddScoped<
            ICategoryRepository,
            CategoryRepository>();

        services.AddScoped<
            ITagRepository,
            TagRepository>();

        services.AddScoped<
            IKnowledgeSourceRepository,
            KnowledgeSourceRepository>();

        services.AddHttpClient<
            IEmbeddingService,
            OllamaEmbeddingService>(
                client =>
                {
                    client.BaseAddress =
                        new Uri("http://localhost:11434");
                });

        services.AddScoped<
            IVectorSearchService,
            VectorSearchService>();

        services.AddScoped<
            IContextBuilderService,
            ContextBuilderService>();

        services.AddHttpClient<
            IChatCompletionService,
            OllamaChatCompletionService>(
                client =>
                {
                    client.BaseAddress =
                        new Uri(
                            "http://localhost:11434");

                    client.Timeout =
                        TimeSpan.FromMinutes(5);
                });

        services.AddMemoryCache();

        services.AddHostedService<
            EmbeddingCacheWarmupService>();

        services.AddScoped<RagService>();

        return services;
    }
}
