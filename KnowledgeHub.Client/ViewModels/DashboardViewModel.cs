using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnowledgeHub.Client.Services;

namespace KnowledgeHub.Client.ViewModels;

public partial class DashboardViewModel
    : ObservableObject
{
    private readonly StatisticsApiService _service;

    [ObservableProperty]
    private int documentsCount;

    [ObservableProperty]
    private int categoriesCount;

    [ObservableProperty]
    private int sourcesCount;

    [ObservableProperty]
    private int tagsCount;

    [ObservableProperty]
    private int chunksCount;

    [ObservableProperty]
    private bool isLoading;

    public DashboardViewModel(
        StatisticsApiService service)
    {
        _service = service;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            IsLoading = true;

            var stats =
                await _service.GetStatisticsAsync();

            DocumentsCount =
                stats.DocumentsCount;

            CategoriesCount =
                stats.CategoriesCount;

            SourcesCount =
                stats.SourcesCount;

            TagsCount =
                stats.TagsCount;

            ChunksCount =
                stats.ChunksCount;
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert(
                "Erreur",
                ex.Message,
                "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }
}