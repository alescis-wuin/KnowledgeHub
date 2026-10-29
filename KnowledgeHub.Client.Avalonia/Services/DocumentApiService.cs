using System.Net.Http.Json;
using KnowledgeHub.Client.Avalonia.Models;

namespace KnowledgeHub.Client.Avalonia.Services;

public class DocumentApiService
{
    private readonly HttpClient _httpClient;

    public DocumentApiService(
        HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<DocumentDto>>
        GetDocumentsAsync()
    {
        var response =
            await _httpClient.GetFromJsonAsync<
                ApiResponse<List<DocumentDto>>>(
                    "api/documents");

        return response?.Data
            ?? [];
    }

    public async Task<List<DocumentDto>>
        SearchDocumentsAsync(
            string searchTerm)
    {
        var response =
            await _httpClient.GetFromJsonAsync<
                ApiResponse<List<DocumentDto>>>(
                    $"api/documents/search?q={Uri.EscapeDataString(searchTerm)}");

        return response?.Data
            ?? [];
    }

    public async Task<DocumentDetailsDto?>
    GetDocumentDetailsAsync(
        Guid id)
    {
        var response =
            await _httpClient.GetFromJsonAsync<
                ApiResponse<DocumentDetailsDto>>(
                    $"api/documents/{id}/details");

        return response?.Data;
    }
}