
using System.Net.Http.Json;
using KnowledgeHub.Client.Avalonia.Models;

namespace KnowledgeHub.Client.Avalonia.Services;

public class DocumentApiService
{
    private readonly HttpClient _httpClient;

    public DocumentApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<DocumentDto>> GetDocumentsAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetFromJsonAsync<
            ApiResponse<List<DocumentDto>>>(
                "api/documents",
                cancellationToken);

        return response?.Data ?? [];
    }

    public async Task<List<DocumentDto>> SearchDocumentsAsync(
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        var uri =
            $"api/documents/search?q={Uri.EscapeDataString(searchTerm)}";

        var response = await _httpClient.GetFromJsonAsync<
            ApiResponse<List<DocumentDto>>>(
                uri,
                cancellationToken);

        return response?.Data ?? [];
    }

    public async Task<DocumentDetailsDto?> GetDocumentDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetFromJsonAsync<
            ApiResponse<DocumentDetailsDto>>(
                $"api/documents/{id}/details",
                cancellationToken);

        return response?.Data;
    }
}
