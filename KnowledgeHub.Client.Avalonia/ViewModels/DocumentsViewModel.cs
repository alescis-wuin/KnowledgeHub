
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnowledgeHub.Client.Avalonia.Models;
using KnowledgeHub.Client.Avalonia.Services;

namespace KnowledgeHub.Client.Avalonia.ViewModels;

public partial class DocumentsViewModel : ViewModelBase
{
    private readonly DocumentApiService _service;

    private CancellationTokenSource? _activeRequest;
    private bool _hasLoaded;

    public ObservableCollection<DocumentDto> Documents { get; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TagsText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool HasNoResults { get; set; }

    [ObservableProperty]
    public partial bool IsDetailsOpen { get; set; }

    [ObservableProperty]
    public partial DocumentDetailsDto? Details { get; set; }

    public bool IsListVisible => !IsDetailsOpen;

    public DocumentsViewModel(DocumentApiService service)
    {
        _service = service;
    }

    partial void OnIsDetailsOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(IsListVisible));
    }

    public Task EnsureLoadedAsync()
    {
        return _hasLoaded
            ? Task.CompletedTask
            : LoadAsync();
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task SearchAsync() => LoadAsync();

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task RefreshAsync() => LoadAsync();

    public async Task LoadAsync()
    {
        using var request = BeginRequest();

        var query = SearchText.Trim();

        HasNoResults = false;

        try
        {
            var documents = query.Length == 0
                ? await _service.GetDocumentsAsync(request.Token)
                : await _service.SearchDocumentsAsync(
                    query,
                    request.Token);

            request.Token.ThrowIfCancellationRequested();

            Documents.Clear();

            foreach (var document in documents)
            {
                Documents.Add(document);
            }

            HasNoResults = Documents.Count == 0;
            _hasLoaded = true;
        }
        catch (OperationCanceledException)
            when (request.IsCancellationRequested)
        {
            // A newer request or navigation canceled this operation.
        }
        catch (OperationCanceledException)
        {
            if (IsCurrentRequest(request))
            {
                ErrorMessage = "La requête a expiré.";
            }
        }
        catch (HttpRequestException ex)
        {
            if (IsCurrentRequest(request))
            {
                ErrorMessage = $"Erreur HTTP : {ex.Message}";
            }
        }
        catch (Exception ex)
        {
            if (IsCurrentRequest(request))
            {
                ErrorMessage = $"Erreur de chargement : {ex.Message}";
            }
        }
        finally
        {
            CompleteRequest(request);
        }
    }

    public async Task OpenDetailsAsync(Guid documentId)
    {
        using var request = BeginRequest();

        IsDetailsOpen = true;
        Details = null;
        TagsText = string.Empty;

        try
        {
            var details = await _service.GetDocumentDetailsAsync(
                documentId,
                request.Token);

            request.Token.ThrowIfCancellationRequested();

            if (details is null)
            {
                ErrorMessage = "Document introuvable.";
                return;
            }

            Details = details;

            TagsText = details.Tags.Count == 0
                ? "Aucun tag"
                : string.Join(", ", details.Tags);
        }
        catch (OperationCanceledException)
            when (request.IsCancellationRequested)
        {
            // Cancellation is expected during navigation.
        }
        catch (OperationCanceledException)
        {
            if (IsCurrentRequest(request))
            {
                ErrorMessage =
                    "Le chargement du document a expiré.";
            }
        }
        catch (HttpRequestException ex)
        {
            if (IsCurrentRequest(request))
            {
                ErrorMessage =
                    $"Impossible de charger le document : {ex.Message}";
            }
        }
        catch (Exception ex)
        {
            if (IsCurrentRequest(request))
            {
                ErrorMessage = $"Erreur : {ex.Message}";
            }
        }
        finally
        {
            CompleteRequest(request);
        }
    }

    public void CancelPendingRequests()
    {
        _activeRequest?.Cancel();
        _activeRequest = null;

        IsLoading = false;
        IsDetailsOpen = false;

        Details = null;
        TagsText = string.Empty;
        ErrorMessage = string.Empty;
    }

    [RelayCommand]
    private void BackToList()
    {
        CancelPendingRequests();
    }

    private CancellationTokenSource BeginRequest()
    {
        _activeRequest?.Cancel();

        var request = new CancellationTokenSource();

        _activeRequest = request;

        IsLoading = true;
        ErrorMessage = string.Empty;

        return request;
    }

    private bool IsCurrentRequest(
        CancellationTokenSource request)
    {
        return ReferenceEquals(_activeRequest, request)
            && !request.IsCancellationRequested;
    }

    private void CompleteRequest(
        CancellationTokenSource request)
    {
        if (!ReferenceEquals(_activeRequest, request))
        {
            return;
        }

        _activeRequest = null;
        IsLoading = false;
    }
}
