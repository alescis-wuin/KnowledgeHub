using System.Net.Http.Json;
using KnowledgeHub.Client.Avalonia.Models;

namespace KnowledgeHub.Client.Avalonia.Services;

public class CategoryApiService
{
    private readonly HttpClient _httpClient;

    public CategoryApiService(
        HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<CategoryDto>>
        GetCategoriesAsync()
    {
        var result =
            await _httpClient.GetFromJsonAsync<
                ApiResponse<List<CategoryDto>>>(
                    "api/categories");

        return result?.Data
            ?? [];
    }
}