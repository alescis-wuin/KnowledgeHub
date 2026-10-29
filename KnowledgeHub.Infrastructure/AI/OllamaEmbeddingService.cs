using System.Net.Http.Json;
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
        var response =
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

        return result?.Embedding
            ?? [];
    }

    private sealed class OllamaEmbeddingResponse
    {
        public float[] Embedding
        {
            get;
            set;
        }
        = [];
    }
}