using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnowledgeHub.Client.Models;
using KnowledgeHub.Client.Services;
using System.Collections.ObjectModel;

namespace KnowledgeHub.Client.ViewModels;

public partial class DocumentsViewModel
    : ObservableObject
{
    private readonly DocumentApiService _service;

    public ObservableCollection<DocumentDto>
        Documents
    {
        get;
    }
    = [];

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private bool noResults;

    public DocumentsViewModel(
        DocumentApiService service)
    {
        _service = service;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            IsLoading = true;

            NoResults = false;

            Documents.Clear();

            var documents =
                await _service.GetDocumentsAsync();

            foreach (var document in documents)
            {
                Documents.Add(document);
            }

            NoResults =
                Documents.Count == 0;
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert(
                "Erreur",
                ex.Message,
                "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task SearchAsync()
    {
        try
        {
            IsLoading = true;

            Documents.Clear();

            if (string.IsNullOrWhiteSpace(
                SearchText))
            {
                await LoadAsync();

                return;
            }

            var documents =
                await _service.SearchDocumentsAsync(
                    SearchText);

            foreach (var document in documents)
            {
                Documents.Add(document);
            }

            NoResults =
                Documents.Count == 0;
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert(
                "Erreur",
                ex.Message,
                "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }
}