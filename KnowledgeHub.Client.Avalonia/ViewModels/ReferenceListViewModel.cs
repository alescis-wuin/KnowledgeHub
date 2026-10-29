
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;

namespace KnowledgeHub.Client.Avalonia.ViewModels;

public sealed record ReferenceItem(
    string Name,
    string? Subtitle = null)
{
    public bool HasSubtitle =>
        !string.IsNullOrWhiteSpace(Subtitle);
}

public sealed class ReferenceListViewModel : ViewModelBase
{
    private readonly Func<Task<IReadOnlyList<ReferenceItem>>>
        _loadItems;

    private bool _hasLoaded;
    private bool _isLoading;
    private bool _hasNoResults;
    private string _errorMessage = string.Empty;

    public string Title { get; }

    public ObservableCollection<ReferenceItem> Items { get; } = [];

    public bool HasItems => Items.Count > 0;

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public bool HasNoResults
    {
        get => _hasNoResults;
        private set => SetProperty(ref _hasNoResults, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public IAsyncRelayCommand RefreshCommand { get; }

    public ReferenceListViewModel(
        string title,
        Func<Task<IReadOnlyList<ReferenceItem>>> loadItems)
    {
        Title = title;
        _loadItems = loadItems;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
    }

    public Task EnsureLoadedAsync()
    {
        return _hasLoaded
            ? Task.CompletedTask
            : RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        HasNoResults = false;

        try
        {
            var items = await _loadItems();

            Items.Clear();

            foreach (var item in items.OrderBy(
                x => x.Name,
                StringComparer.CurrentCultureIgnoreCase))
            {
                Items.Add(item);
            }

            OnPropertyChanged(nameof(HasItems));

            HasNoResults = Items.Count == 0;
            _hasLoaded = true;
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage =
                $"Impossible de contacter l'API : {ex.Message}";
        }
        catch (OperationCanceledException)
        {
            ErrorMessage =
                "Le chargement a expiré ou a été annulé.";
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
