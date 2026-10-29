
using Avalonia.Controls;
using KnowledgeHub.Client.Avalonia.ViewModels;

namespace KnowledgeHub.Client.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Opened += async (_, _) =>
        {
            if (DataContext is MainViewModel viewModel)
            {
                await viewModel.InitializeAsync();
            }
        };
    }
}
