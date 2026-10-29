using System.Net.Http.Json;
using KnowledgeHub.Client.Avalonia.Models;

namespace KnowledgeHub.Client.Avalonia.Services;

public class TagApiService
{
    private readonly HttpClient _httpClient;

    public TagApiService(
        HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<TagDto>>
        GetTagsAsync()
    {
        var result =
            await _httpClient.GetFromJsonAsync<
                ApiResponse<List<TagDto>>>(
                    "api/tags");

        return result?.Data
            ?? [];
    }
}