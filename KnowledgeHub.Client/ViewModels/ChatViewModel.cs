using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnowledgeHub.Client.Models;
using KnowledgeHub.Client.Services;

namespace KnowledgeHub.Client.ViewModels;

public partial class ChatViewModel
    : ObservableObject
{
    private readonly RagApiClient
        _ragApiClient;

    public ChatViewModel(
        RagApiClient ragApiClient)
    {
        _ragApiClient =
            ragApiClient;
    }

    [ObservableProperty]
    private string question =
        string.Empty;

    [ObservableProperty]
    private string answer =
        string.Empty;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private IReadOnlyCollection<
        SourceReferenceDto>
        sources
        = [];

    [RelayCommand]
    private async Task AskAsync()
    {
        if (string.IsNullOrWhiteSpace(
                Question))
        {
            return;
        }

        try
        {
            IsLoading = true;

            Answer = string.Empty;

            Sources = [];

            var result =
                await _ragApiClient
                    .AskAsync(
                        Question);

            Answer =
                result.Answer;

            Sources =
                result.Sources;
        }
        finally
        {
            IsLoading = false;
        }
    }
}