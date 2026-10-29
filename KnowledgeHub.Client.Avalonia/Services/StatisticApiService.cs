using System.Net.Http.Json;
using KnowledgeHub.Client.Avalonia.Models;

namespace KnowledgeHub.Client.Avalonia.Services;

public class StatisticsApiService
{
    private readonly HttpClient _httpClient;

    public StatisticsApiService(
        HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<KnowledgeStatisticsDto>
        GetStatisticsAsync()
    {
        var response =
            await _httpClient.GetFromJsonAsync<
                ApiResponse<KnowledgeStatisticsDto>>(
                    "api/statistics");

        return response?.Data
            ?? new KnowledgeStatisticsDto();
    }
}