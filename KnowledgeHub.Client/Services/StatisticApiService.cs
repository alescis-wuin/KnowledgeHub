using System.Net.Http.Json;
using KnowledgeHub.Client.Models;

namespace KnowledgeHub.Client.Services;

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