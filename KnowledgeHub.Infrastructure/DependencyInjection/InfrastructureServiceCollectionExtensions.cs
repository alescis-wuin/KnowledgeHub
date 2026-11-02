using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Application.Services;
using KnowledgeHub.Application.Verification;
using KnowledgeHub.Domain.Interfaces;
using KnowledgeHub.Infrastructure.AI;
using KnowledgeHub.Infrastructure.Persistence;
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

        services.AddSingleton<VectorIndexCache>();
        services.AddSingleton<IVectorIndexCacheInvalidator>(
            provider => provider.GetRequiredService<VectorIndexCache>());

        services.AddHostedService<
            EmbeddingCacheWarmupService>();

        services.AddScoped<RagService>();

        services.AddScoped<IEvidenceSnapshotProvider, EvidenceSnapshotProvider>();

        services.AddHttpClient<IClaimProposalGenerator, OllamaClaimProposalGenerator>(
            client =>
            {
                client.BaseAddress = new Uri("http://localhost:11434");
                client.Timeout = TimeSpan.FromMinutes(3);
            });

        services.AddScoped(provider => new VerifiedVerdictService(
            provider.GetRequiredService<IEvidenceSnapshotProvider>(),
            provider.GetService<IQualifiedEvidenceAssessor>(),
            configuration["Verification:TrustedSemanticPolicyId"] ?? string.Empty));

        services.AddScoped(provider => new VerifiedRagService(
            provider.GetRequiredService<IVectorSearchService>(),
            provider.GetRequiredService<IEvidenceSnapshotProvider>(),
            provider.GetRequiredService<IClaimProposalGenerator>(),
            provider.GetRequiredService<VerifiedVerdictService>(),
            provider.GetService<IQualifiedEvidenceAssessor>() is not null &&
                !string.IsNullOrWhiteSpace(
                    configuration["Verification:TrustedSemanticPolicyId"])));

        return services;
    }
}
