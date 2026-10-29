using System.Net.Http.Json;
using KnowledgeHub.Client.Avalonia.Models;

namespace KnowledgeHub.Client.Avalonia.Services;

public sealed class RagApiClient
{
    private readonly HttpClient
        _httpClient;

    public RagApiClient(
        HttpClient httpClient)
    {
        _httpClient =
            httpClient;
    }

    public async Task<
        AskQuestionResponseDto>
        AskAsync(
            string question,
            CancellationToken cancellationToken =
                default)
    {
        var request =
            new AskQuestionRequestDto
            {
                Question = question
            };

        var response =
            await _httpClient.PostAsJsonAsync(
                "api/rag/ask",
                request,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content
                   .ReadFromJsonAsync<
                       AskQuestionResponseDto>(
                           cancellationToken)
               ?? new AskQuestionResponseDto();
    }
}