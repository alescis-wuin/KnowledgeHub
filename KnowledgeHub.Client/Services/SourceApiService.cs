using System.Net.Http.Json;
using KnowledgeHub.Client.Models;

namespace KnowledgeHub.Client.Services;

public class SourceApiService
{
    private readonly HttpClient _httpClient;

    public SourceApiService(
        HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<SourceDto>>
        GetSourcesAsync()
    {
        var result =
            await _httpClient.GetFromJsonAsync<
                ApiResponse<List<SourceDto>>>(
                    "api/sources");

        return result?.Data
            ?? [];
    }
}