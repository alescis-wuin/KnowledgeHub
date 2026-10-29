
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnowledgeHub.Client.Avalonia.Services;

namespace KnowledgeHub.Client.Avalonia.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly StatisticsApiService _service;

    public DashboardViewModel(StatisticsApiService service)
    {
        _service = service;
    }

    [ObservableProperty]
    public partial int DocumentsCount { get; set; }

    [ObservableProperty]
    public partial int CategoriesCount { get; set; }

    [ObservableProperty]
    public partial int TagsCount { get; set; }

    [ObservableProperty]
    public partial int SourcesCount { get; set; }

    [ObservableProperty]
    public partial int ChunksCount { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; }
        = string.Empty;

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var statistics =
                await _service.GetStatisticsAsync();

            DocumentsCount = statistics.DocumentsCount;
            CategoriesCount = statistics.CategoriesCount;
            TagsCount = statistics.TagsCount;
            SourcesCount = statistics.SourcesCount;
            ChunksCount = statistics.ChunksCount;
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage =
                $"Impossible de contacter l'API : {ex.Message}";
        }
        catch (TaskCanceledException)
        {
            ErrorMessage =
                "La requête vers l'API a expiré ou a été annulée.";
        }
        catch (Exception ex)
        {
            ErrorMessage =
                $"Erreur lors du chargement : {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
