
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnowledgeHub.Client.Avalonia.Models;
using KnowledgeHub.Client.Avalonia.Services;

namespace KnowledgeHub.Client.Avalonia.ViewModels;

public partial class DocumentsViewModel : ViewModelBase
{
    private readonly DocumentApiService _service;
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
        return _hasLoaded ? Task.CompletedTask : LoadAsync();
    }

    [RelayCommand]
    private Task SearchAsync() => LoadAsync();

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    public async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var documents = string.IsNullOrWhiteSpace(SearchText)
                ? await _service.GetDocumentsAsync()
                : await _service.SearchDocumentsAsync(SearchText.Trim());

            Documents.Clear();

            foreach (var document in documents)
            {
                Documents.Add(document);
            }

            HasNoResults = Documents.Count == 0;
            _hasLoaded = true;
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"Erreur HTTP : {ex.Message}";
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = "La requête a expiré ou a été annulée.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erreur de chargement : {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task OpenDetailsAsync(Guid documentId)
    {
        if (IsLoading)
        {
            return;
        }

        IsDetailsOpen = true;
        IsLoading = true;
        ErrorMessage = string.Empty;
        Details = null;
        TagsText = string.Empty;

        try
        {
            var details =
                await _service.GetDocumentDetailsAsync(documentId);

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
        catch (HttpRequestException ex)
        {
            ErrorMessage =
                $"Impossible de charger le document : {ex.Message}";
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = "Le chargement du document a expiré.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erreur : {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void BackToList()
    {
        IsDetailsOpen = false;
        Details = null;
        TagsText = string.Empty;
        ErrorMessage = string.Empty;
    }
}
