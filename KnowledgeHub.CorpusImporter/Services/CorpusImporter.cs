using System.Net.Http.Json;
using KnowledgeHub.Application.DTOs;
using KnowledgeHub.CorpusImporter.Models;

namespace KnowledgeHub.CorpusImporter.Services;

/// <summary>
/// HTTP client for the API's document lifecycle. This assembly has no database
/// or embedding access: only the API may persist and invalidate vector indexes.
/// A failed request ends the run; a subsequent run can resume safely.
/// </summary>
public sealed class CorpusImporter
{
    private readonly HttpClient _http;

    public CorpusImporter(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        if (_http.BaseAddress is null)
        {
            throw new ArgumentException("An absolute API base address is required.", nameof(http));
        }
    }

    public async Task<CorpusImportReport> ImportAsync(
        CorpusRoot corpus,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentNullException.ThrowIfNull(corpus.Categories);
        ArgumentNullException.ThrowIfNull(corpus.Sources);
        ArgumentNullException.ThrowIfNull(corpus.Tags);
        ArgumentNullException.ThrowIfNull(corpus.Documents);

        var categories = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var item in await GetAsync<List<CategoryDto>>(
                     "api/categories", cancellationToken))
        {
            categories.TryAdd(item.Name, item.Id);
        }

        var sources = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var item in await GetAsync<List<KnowledgeSourceDto>>(
                     "api/sources", cancellationToken))
        {
            sources.TryAdd(item.Name, item.Id);
        }

        var tags = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var item in await GetAsync<List<TagDto>>(
                     "api/tags", cancellationToken))
        {
            tags.TryAdd(item.Name, item.Id);
        }

        var categoriesCreated = 0;
        foreach (var name in corpus.Categories
                     .Select(item => item.Name?.Trim())
                     .Where(name => !string.IsNullOrWhiteSpace(name))
                     .Select(name => name!)
                     .Distinct(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (categories.ContainsKey(name)) continue;

            var created = await PostAsync<CategoryDto>(
                "api/categories", new CreateCategoryDto { Name = name },
                cancellationToken);
            categories.Add(name, created.Id);
            categoriesCreated++;
        }

        var sourcesCreated = 0;
        foreach (var item in corpus.Sources
                     .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                     .DistinctBy(item => item.Name.Trim(), StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = item.Name.Trim();
            if (sources.ContainsKey(name)) continue;

            var created = await PostAsync<KnowledgeSourceDto>(
                "api/sources",
                new CreateKnowledgeSourceDto { Name = name, Url = item.Url },
                cancellationToken);
            sources.Add(name, created.Id);
            sourcesCreated++;
        }

        var tagsCreated = 0;
        foreach (var name in corpus.Tags
                     .Select(item => item.Name?.Trim())
                     .Where(name => !string.IsNullOrWhiteSpace(name))
                     .Select(name => name!)
                     .Distinct(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (tags.ContainsKey(name)) continue;

            var created = await PostAsync<TagDto>(
                "api/tags", new CreateTagDto { Name = name },
                cancellationToken);
            tags.Add(name, created.Id);
            tagsCreated++;
        }

        var existing = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var document in await GetAsync<List<DocumentDto>>(
                     "api/documents", cancellationToken))
        {
            existing.TryAdd(document.Title, document.Id);
        }

        var createdCount = 0;
        var repaired = 0;
        var unchanged = 0;
        var ignored = 0;

        foreach (var item in corpus.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(item.Title) ||
                string.IsNullOrWhiteSpace(item.Content))
            {
                ignored++;
                continue;
            }

            var title = item.Title.Trim();
            if (existing.TryGetValue(title, out var id))
            {
                var wasRepaired = await PostAsync<bool>(
                    $"api/documents/{id:D}/ensure-index", new { },
                    cancellationToken);
                if (wasRepaired) repaired++;
                else unchanged++;
                continue;
            }

            Guid? categoryId = categories.TryGetValue(
                item.CategoryName?.Trim() ?? string.Empty, out var category)
                ? category : null;
            Guid? sourceId = sources.TryGetValue(
                item.SourceName?.Trim() ?? string.Empty, out var source)
                ? source : null;

            var tagIds = (item.TagNames ?? [])
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => tags.TryGetValue(name.Trim(), out var tag)
                    ? (Guid?)tag : null)
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .Distinct()
                .ToList();

            var result = await PostAsync<DocumentDto>(
                "api/documents",
                new CreateDocumentDto
                {
                    Title = title,
                    Content = item.Content,
                    Summary = item.Summary,
                    SearchKeywords = item.SearchKeywords,
                    CategoryId = categoryId,
                    SourceId = sourceId,
                    TagIds = tagIds
                },
                cancellationToken);

            existing.Add(title, result.Id);
            createdCount++;
        }

        return new CorpusImportReport(
            createdCount, repaired, unchanged, ignored,
            categoriesCreated, sourcesCreated, tagsCreated);
    }

    private Task<T> GetAsync<T>(
        string route, CancellationToken cancellationToken)
    {
        return SendAsync<T>(new HttpRequestMessage(HttpMethod.Get, route),
            cancellationToken);
    }

    private Task<T> PostAsync<T>(
        string route, object body, CancellationToken cancellationToken)
    {
        return SendAsync<T>(new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = JsonContent.Create(body)
        }, cancellationToken);
    }

    private async Task<T> SendAsync<T>(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using (request)
        using (var response = await _http.SendAsync(
                   request, HttpCompletionOption.ResponseHeadersRead,
                   cancellationToken))
        {
            // Never log document payloads or confidential API response bodies.
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"API {request.Method} {request.RequestUri}: HTTP " +
                    $"{(int)response.StatusCode} ({response.ReasonPhrase}).",
                    null, response.StatusCode);
            }

            var envelope = await response.Content
                .ReadFromJsonAsync<ApiResponse<T>>(
                    cancellationToken: cancellationToken);

            if (envelope is null || !envelope.Success)
            {
                throw new InvalidOperationException(
                    $"API {request.Method} {request.RequestUri} returned an invalid response.");
            }

            // Do not use envelope?.Data: T also represents value types (bool).
            var data = envelope.Data;
            if (data is null)
            {
                throw new InvalidOperationException(
                    $"API {request.Method} {request.RequestUri} returned an invalid response.");
            }

            return data;
        }
    }
}

public sealed record CorpusImportReport(
    int Created,
    int Repaired,
    int Unchanged,
    int Ignored,
    int CategoriesCreated,
    int SourcesCreated,
    int TagsCreated);
