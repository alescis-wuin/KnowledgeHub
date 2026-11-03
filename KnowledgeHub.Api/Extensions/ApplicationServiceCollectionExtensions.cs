using KnowledgeHub.Application.Identity;
using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Application.Services;
using Microsoft.Extensions.Configuration;

namespace KnowledgeHub.Api.Extensions;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services)
    {
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<IKnowledgeSourceService, KnowledgeSourceService>();
        services.AddScoped<ITextChunker, DefaultTextChunker>();
        services.AddScoped<IChunkEmbeddingGenerator, ChunkEmbeddingGenerator>();
        services.AddScoped<DocumentIndexRepairService>();

        // Empty allowlist is intentional: until KH-009.1.4 wires JWT Bearer,
        // identity resolution is fail-closed and does not authenticate requests.
        services.AddScoped<IAuthenticatedUserIdentityResolver>(provider =>
            new OidcPrincipalIdentityResolver(
                provider.GetRequiredService<IConfiguration>()
                    .GetSection("Authentication:TrustedIssuers")
                    .GetChildren()
                    .Select(child => child.Value ?? string.Empty)));

        return services;
    }
}
