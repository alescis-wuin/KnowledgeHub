using KnowledgeHub.Client.ViewModels;

namespace KnowledgeHub.Client.Views;

public partial class TagsPage : ContentPage
{
    private readonly TagsViewModel _viewModel;

    public TagsPage(
        TagsViewModel viewModel)
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