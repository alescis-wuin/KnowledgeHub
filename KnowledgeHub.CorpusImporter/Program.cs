using KnowledgeHub.CorpusImporter.Services;
using Microsoft.Extensions.Logging;

if (args.Length > 1)
{
    throw new ArgumentException("Usage: KnowledgeHub.CorpusImporter [corpus.json]");
}

var corpusPath = Path.GetFullPath(args.Length == 1
    ? args[0]
    : Path.Combine("Data", "KnowledgeHub-corpus-documentaire.json"));

var configuredUrl = Environment.GetEnvironmentVariable("KNOWLEDGEHUB_API_URL");
if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var apiUri) ||
    apiUri.UserInfo.Length != 0 ||
    apiUri.Query.Length != 0 ||
    apiUri.Fragment.Length != 0 ||
    (apiUri.Scheme != Uri.UriSchemeHttps && apiUri.Scheme != Uri.UriSchemeHttp))
{
    throw new InvalidOperationException(
        "Set KNOWLEDGEHUB_API_URL to an absolute HTTP(S) URL for the API.");
}

// Cleartext HTTP must never carry document content to a non-local host.
if (apiUri.Scheme == Uri.UriSchemeHttp &&
    !apiUri.IsLoopback &&
    !string.Equals(apiUri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "HTTP is permitted only for a loopback API. Use HTTPS for remote hosts.");
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

var corpus = await new CorpusReader().ReadAsync(corpusPath);
using var handler = new HttpClientHandler { AllowAutoRedirect = false };
using var http = new HttpClient(handler)
{
    BaseAddress = new Uri(configuredUrl!.TrimEnd('/') + "/"),
    Timeout = TimeSpan.FromMinutes(30)
};

// Importer logs are JSONL as well; do not output corpus paths or payloads.
using var loggerFactory = LoggerFactory.Create(logging =>
{
    logging.SetMinimumLevel(LogLevel.Information);
    logging.AddJsonConsole(options =>
    {
        options.IncludeScopes = true;
        options.TimestampFormat = "O";
    });
});
var logger = loggerFactory.CreateLogger<CorpusImporter>();
logger.LogInformation("Corpus import starting with {DocumentCount} documents",
    corpus.Documents.Count);

var report = await new CorpusImporter(http, logger).ImportAsync(
    corpus,
    cancellation.Token);

logger.LogInformation(
    "Corpus metadata created: {CategoriesCreated} categories, " +
    "{SourcesCreated} sources, {TagsCreated} tags",
    report.CategoriesCreated, report.SourcesCreated, report.TagsCreated);
