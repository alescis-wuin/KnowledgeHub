using CommunityToolkit.Mvvm.ComponentModel;
using KnowledgeHub.Client.Models;
using KnowledgeHub.Client.Services;

namespace KnowledgeHub.Client.ViewModels;

public partial class DocumentDetailsViewModel
    : ObservableObject
{
    private readonly DocumentApiService _service;

    [ObservableProperty]
    private DocumentDetailsDto? document;

    [ObservableProperty]
    private bool isLoading;

    public DocumentDetailsViewModel(
        DocumentApiService service)
    {
        _service = service;
    }

    public async Task LoadAsync(
        Guid documentId)
    {
        try
        {
            IsLoading = true;

            Document =
                await _service
                    .GetDocumentDetailsAsync(
                        documentId);
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