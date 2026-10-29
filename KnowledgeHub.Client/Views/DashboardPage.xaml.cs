using KnowledgeHub.Client.ViewModels;

namespace KnowledgeHub.Client.Views;

public partial class DashboardPage
    : ContentPage
{
    private readonly DashboardViewModel _viewModel;

    public DashboardPage(
        DashboardViewModel viewModel)
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