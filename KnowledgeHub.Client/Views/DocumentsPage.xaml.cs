using KnowledgeHub.Client.Models;
using KnowledgeHub.Client.ViewModels;

namespace KnowledgeHub.Client.Views;

public partial class DocumentsPage
    : ContentPage
{
    private readonly DocumentsViewModel _viewModel;

    public DocumentsPage(
        DocumentsViewModel viewModel)
    {
        InitializeComponent();

        BindingContext = viewModel;

        _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_viewModel.Documents.Count == 0)
        {
            await _viewModel.LoadAsync();
        }
    }

    private async void DocumentsSelectionChanged(
    object sender,
    SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault()
            is DocumentDto document)
        {
            await Shell.Current.GoToAsync(
                $"{nameof(DocumentDetailsPage)}?DocumentId={document.Id}");
        }
    }
}