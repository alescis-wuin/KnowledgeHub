using CommunityToolkit.Maui;
using KnowledgeHub.Client.Services;
using KnowledgeHub.Client.ViewModels;
using KnowledgeHub.Client.Views;
using Microsoft.Extensions.Configuration;

namespace KnowledgeHub.Client;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit();

        ConfigureApplicationSettings(builder);
        ConfigureServices(builder.Services, builder.Configuration);

        return builder.Build();
    }

    private static void ConfigureApplicationSettings(
        MauiAppBuilder builder)
    {
        builder.Configuration
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile(
                "appsettings.Development.json",
                optional: false,
                reloadOnChange: false);
    }

    private static void ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var apiBaseUri = GetApiBaseUri(configuration);

        ConfigureHttpClients(services, apiBaseUri);
        RegisterApiServices(services);
        RegisterViewModels(services);
        RegisterPages(services);
        RegisterApplicationShell(services);
    }

    private static Uri GetApiBaseUri(
        IConfiguration configuration)
    {
        var configuredBaseUrl =
            configuration["Api:BaseUrl"];

        if (string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            throw new InvalidOperationException(
                "La propriété de configuration " +
                "'Api:BaseUrl' est manquante ou vide.");
        }

        configuredBaseUrl = configuredBaseUrl.Trim();

        if (!configuredBaseUrl.EndsWith(
                "/",
                StringComparison.Ordinal))
        {
            configuredBaseUrl += "/";
        }

        if (!Uri.TryCreate(
                configuredBaseUrl,
                UriKind.Absolute,
                out var apiBaseUri))
        {
            throw new InvalidOperationException(
                $"La valeur Api:BaseUrl " +
                $"'{configuredBaseUrl}' n'est pas une URI absolue valide.");
        }

        if (apiBaseUri.Scheme != Uri.UriSchemeHttp &&
            apiBaseUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "La propriété Api:BaseUrl doit utiliser " +
                "le protocole HTTP ou HTTPS.");
        }

        return apiBaseUri;
    }

    private static void ConfigureHttpClients(
        IServiceCollection services,
        Uri apiBaseUri)
    {
        /*
         * Client HTTP partagé par les services existants :
         * DocumentApiService, StatisticsApiService,
         * CategoryApiService, TagApiService et SourceApiService.
         */
        services.AddSingleton(
            _ => new HttpClient
            {
                BaseAddress = apiBaseUri,
                Timeout = TimeSpan.FromMinutes(2)
            });

        /*
         * Client typé consacré au pipeline RAG.
         *
         * Le délai est volontairement plus long, car la génération
         * locale par Ollama peut prendre plusieurs dizaines de secondes.
         */
        services.AddHttpClient<RagApiClient>(
            client =>
            {
                client.BaseAddress = apiBaseUri;
                client.Timeout = TimeSpan.FromMinutes(5);
            });
    }

    private static void RegisterApiServices(
        IServiceCollection services)
    {
        services.AddSingleton<DocumentApiService>();
        services.AddSingleton<StatisticsApiService>();
        services.AddSingleton<CategoryApiService>();
        services.AddSingleton<TagApiService>();
        services.AddSingleton<SourceApiService>();

        /*
         * ChatApiService n'est volontairement plus enregistré.
         * RagApiClient devient l'unique client pour /api/rag/ask.
         */
    }

    private static void RegisterViewModels(
        IServiceCollection services)
    {
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<DocumentsViewModel>();
        services.AddTransient<DocumentDetailsViewModel>();
        services.AddTransient<CategoriesViewModel>();
        services.AddTransient<TagsViewModel>();
        services.AddTransient<SourcesViewModel>();
        services.AddTransient<ChatViewModel>();
    }

    private static void RegisterPages(
        IServiceCollection services)
    {
        services.AddTransient<DashboardPage>();
        services.AddTransient<DocumentsPage>();
        services.AddTransient<DocumentDetailsPage>();
        services.AddTransient<CategoriesPage>();
        services.AddTransient<TagsPage>();
        services.AddTransient<SourcesPage>();
        services.AddTransient<ChatPage>();
    }

    private static void RegisterApplicationShell(
        IServiceCollection services)
    {
        services.AddSingleton<AppShell>();
    }
}