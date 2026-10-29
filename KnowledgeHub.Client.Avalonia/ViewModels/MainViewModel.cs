
using CommunityToolkit.Mvvm.ComponentModel;
using KnowledgeHub.Client.Avalonia.Services;

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
	    DocumentsViewModel documents,
	    CategoryApiService categoryService,
	    TagApiService tagService,
	    SourceApiService sourceService,
	    ChatViewModel chat)
    {
        _dashboard = dashboard;

        var categories = new ReferenceListViewModel(
            "Catégories",
            async () =>
                (await categoryService.GetCategoriesAsync())
                    .Select(x => new ReferenceItem(x.Name))
                    .ToArray());

        var tags = new ReferenceListViewModel(
            "Tags",
            async () =>
                (await tagService.GetTagsAsync())
                    .Select(x => new ReferenceItem(x.Name))
                    .ToArray());

        var sources = new ReferenceListViewModel(
            "Sources",
            async () =>
                (await sourceService.GetSourcesAsync())
                    .Select(x => new ReferenceItem(
                        x.Name,
                        x.Url))
                    .ToArray());

        Sections =
        [
            new("Tableau de bord", dashboard),
            new("Documents", documents),
            new("Catégories", categories),
            new("Tags", tags),
            new("Sources", sources),
            new("Chat IA", chat)
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
