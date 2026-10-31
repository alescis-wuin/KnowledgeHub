using KnowledgeHub.Application.Telemetry;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using KnowledgeHub.Application.Interfaces;

namespace KnowledgeHub.Infrastructure.AI;

public class OllamaEmbeddingService
    : IEmbeddingService
{
    private readonly HttpClient _httpClient;

    public OllamaEmbeddingService(
        HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<float[]>
        GenerateEmbeddingAsync(
            string text,
            CancellationToken cancellationToken = default)
    {
        return await KnowledgeHubMetrics.MeasureOllamaAsync("embedding", () =>
            KnowledgeHubActivities.TraceAsync("ollama.embed", async () =>
        {
        using var response =
            await _httpClient.PostAsJsonAsync(
                "/api/embeddings",
                new
                {
                    model = "nomic-embed-text",
                    prompt = text
                },
                cancellationToken);

        response.EnsureSuccessStatusCode();

        var result =
            await response.Content
                .ReadFromJsonAsync<
                    OllamaEmbeddingResponse>(
                        cancellationToken);

        if (result is not null)
        {
            OllamaNativeTelemetry.Record("embedding", result.NativeFields);
        }

        return result?.Embedding
            ?? [];
        }, cancellationToken), cancellationToken);
    }

    private sealed class OllamaEmbeddingResponse
    {
        public float[] Embedding
        {
            get;
            set;
        }
        = [];

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? NativeFields { get; set; }
    }
}