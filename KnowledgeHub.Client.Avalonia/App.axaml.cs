
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using KnowledgeHub.Client.Avalonia.Services;
using KnowledgeHub.Client.Avalonia.ViewModels;
using KnowledgeHub.Client.Avalonia.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KnowledgeHub.Client.Avalonia;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile(
                    "appsettings.json",
                    optional: false,
                    reloadOnChange: false)
                .Build();

            var configuredUrl = configuration["Api:BaseUrl"];

            if (string.IsNullOrWhiteSpace(configuredUrl))
            {
                throw new InvalidOperationException(
                    "La configuration Api:BaseUrl est manquante.");
            }

            var normalizedUrl =
                configuredUrl.Trim().TrimEnd('/') + "/";

            if (!Uri.TryCreate(
                    normalizedUrl,
                    UriKind.Absolute,
                    out var apiBaseUri) ||
                (apiBaseUri.Scheme != Uri.UriSchemeHttp &&
                 apiBaseUri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException(
                    "Api:BaseUrl doit être une URL HTTP ou HTTPS valide.");
            }

            var services = new ServiceCollection();

            services.AddSingleton<IConfiguration>(configuration);

            Action<HttpClient> configureStandardClient = client =>
            {
                client.BaseAddress = apiBaseUri;
                client.Timeout = TimeSpan.FromMinutes(2);
            };

            services.AddHttpClient<DocumentApiService>(
                configureStandardClient);

            services.AddHttpClient<StatisticsApiService>(
                configureStandardClient);

            services.AddHttpClient<CategoryApiService>(
                configureStandardClient);

            services.AddHttpClient<TagApiService>(
                configureStandardClient);

            services.AddHttpClient<SourceApiService>(
                configureStandardClient);

            services.AddHttpClient<RagApiClient>(client =>
            {
                client.BaseAddress = apiBaseUri;
                client.Timeout = TimeSpan.FromMinutes(5);
            });

            services.AddTransient<MainViewModel>();

            _serviceProvider = services.BuildServiceProvider();

            desktop.MainWindow = new MainWindow
            {
                DataContext = _serviceProvider
                    .GetRequiredService<MainViewModel>()
            };

            desktop.Exit += (_, _) =>
            {
                _serviceProvider?.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
