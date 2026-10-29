using Avalonia.Controls;
using Avalonia.Interactivity;
using KnowledgeHub.Client.Avalonia.ViewModels;

namespace KnowledgeHub.Client.Avalonia.Views;

public partial class ReferenceListView : UserControl
{
    public ReferenceListView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(
        object? sender,
        RoutedEventArgs e)
    {
        if (DataContext is ReferenceListViewModel viewModel)
        {
            await viewModel.EnsureLoadedAsync();
        }
    }
}
