using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Application.Services;

namespace KnowledgeHub.Api.Extensions;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services)
    {
        services.AddScoped<
            IDocumentService,
            DocumentService>();

        services.AddScoped<
            ICategoryService,
            CategoryService>();

        services.AddScoped<
            ITagService,
            TagService>();

        services.AddScoped<
            IKnowledgeSourceService,
            KnowledgeSourceService>();

        services.AddScoped<
            ITextChunker,
            DefaultTextChunker>();

        return services;
    }
}