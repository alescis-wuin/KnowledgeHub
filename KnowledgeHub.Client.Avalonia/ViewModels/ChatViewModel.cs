
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnowledgeHub.Client.Avalonia.Models;
using KnowledgeHub.Client.Avalonia.Services;

namespace KnowledgeHub.Client.Avalonia.ViewModels;

public partial class ChatViewModel : ViewModelBase
{
    private readonly RagApiClient _ragApiClient;
    private CancellationTokenSource? _requestCancellation;

    public ChatViewModel(RagApiClient ragApiClient)
    {
        _ragApiClient = ragApiClient;
    }

    public ObservableCollection<SourceReferenceDto> Sources { get; } = [];

    [ObservableProperty]
    public partial string Question { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Answer { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    public bool CanAsk =>
        !IsLoading && !string.IsNullOrWhiteSpace(Question);

    public bool HasSources => Sources.Count > 0;

    partial void OnQuestionChanged(string value)
    {
        OnPropertyChanged(nameof(CanAsk));
    }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanAsk));
    }

    [RelayCommand]
    private async Task AskAsync()
    {
        if (!CanAsk)
        {
            return;
        }

        var question = Question.Trim();

        IsLoading = true;
        Answer = string.Empty;
        ErrorMessage = string.Empty;

        Sources.Clear();
        OnPropertyChanged(nameof(HasSources));

        using var cancellation = new CancellationTokenSource();
        _requestCancellation = cancellation;

        try
        {
            var response = await _ragApiClient.AskAsync(
                question,
                cancellation.Token);

            Answer = response.Answer;

            foreach (var source in response.Sources)
            {
                Sources.Add(source);
            }

            OnPropertyChanged(nameof(HasSources));
        }
        catch (OperationCanceledException)
            when (cancellation.IsCancellationRequested)
        {
            ErrorMessage = "La demande a été annulée.";
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "La requête RAG a expiré.";
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage =
                $"Erreur de communication avec l'API : {ex.Message}";
        }
        catch (Exception ex)
        {
            ErrorMessage =
                $"Impossible d'obtenir une réponse : {ex.Message}";
        }
        finally
        {
            _requestCancellation = null;
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _requestCancellation?.Cancel();
    }
}
