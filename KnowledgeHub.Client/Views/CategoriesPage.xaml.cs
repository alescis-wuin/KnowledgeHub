using KnowledgeHub.Client.ViewModels;

namespace KnowledgeHub.Client.Views;

public partial class CategoriesPage : ContentPage
{
    private readonly CategoriesViewModel _viewModel;

    public CategoriesPage(
        CategoriesViewModel viewModel)
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