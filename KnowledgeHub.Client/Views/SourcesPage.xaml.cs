using KnowledgeHub.Client.ViewModels;

namespace KnowledgeHub.Client.Views;

public partial class SourcesPage : ContentPage
{
    private readonly SourcesViewModel _viewModel;

    public SourcesPage(
        SourcesViewModel viewModel)
    {
        InitializeComponent();

        BindingContext = viewModel;

        _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await _viewModel.LoadAsync();
    }
}