using KnowledgeHub.Client.ViewModels;

namespace KnowledgeHub.Client.Views;

[QueryProperty(
    nameof(DocumentId),
    "DocumentId")]
public partial class DocumentDetailsPage
    : ContentPage
{
    private readonly DocumentDetailsViewModel
        _viewModel;

    public string DocumentId
    {
        set
        {
            LoadDocument(value);
        }
    }

    public DocumentDetailsPage(
        DocumentDetailsViewModel viewModel)
    {
        InitializeComponent();

        BindingContext = viewModel;

        _viewModel = viewModel;
    }

    private async void LoadDocument(
        string value)
    {
        if (Guid.TryParse(
            value,
            out var documentId))
        {
            await _viewModel.LoadAsync(
                documentId);
        }
    }

    private async void BackClicked(
    object sender,
    EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}