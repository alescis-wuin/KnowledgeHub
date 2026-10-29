
using CommunityToolkit.Mvvm.ComponentModel;

namespace KnowledgeHub.Client.Avalonia.ViewModels;

public sealed record NavigationItem(
    string Title,
    ViewModelBase Page);

public partial class MainViewModel : ViewModelBase
{
    private readonly DashboardViewModel _dashboard;

    public IReadOnlyList<NavigationItem> Sections { get; }

    [ObservableProperty]
    public partial NavigationItem? SelectedSection
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial ViewModelBase? CurrentPage
    {
        get;
        set;
    }

    public MainViewModel(
        DashboardViewModel dashboard,
        DocumentsViewModel documents)
    {
        _dashboard = dashboard;

        Sections =
        [
            new("Tableau de bord", dashboard),
            new("Documents", documents),
            new(
                "Catégories",
                new PlaceholderViewModel("Catégories")),
            new(
                "Tags",
                new PlaceholderViewModel("Tags")),
            new(
                "Sources",
                new PlaceholderViewModel("Sources")),
            new(
                "Chat IA",
                new PlaceholderViewModel("Chat IA"))
        ];

        SelectedSection = Sections[0];
    }

    partial void OnSelectedSectionChanged(
        NavigationItem? value)
    {
        CurrentPage = value?.Page;
    }

    public Task InitializeAsync()
    {
        return _dashboard.RefreshAsync();
    }
}
