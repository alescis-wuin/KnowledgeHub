
using Avalonia.Controls;
using Avalonia.Interactivity;
using KnowledgeHub.Client.Avalonia.Models;
using KnowledgeHub.Client.Avalonia.ViewModels;

namespace KnowledgeHub.Client.Avalonia.Views;

public partial class DocumentsView : UserControl
{
    public DocumentsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(
        object? sender,
        RoutedEventArgs e)
    {
        if (DataContext is DocumentsViewModel viewModel)
        {
            await viewModel.EnsureLoadedAsync();
        }
    }

    private async void OnDocumentSelectionChanged(
        object? sender,
        SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0 ||
            e.AddedItems[0] is not DocumentDto document)
        {
            return;
        }

        if (sender is ListBox listBox)
        {
            listBox.SelectedItem = null;
        }

        if (DataContext is DocumentsViewModel viewModel)
        {
            await viewModel.OpenDetailsAsync(document.Id);
        }
    }
}
