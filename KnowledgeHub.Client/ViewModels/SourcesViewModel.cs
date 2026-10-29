using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnowledgeHub.Client.Models;
using KnowledgeHub.Client.Services;
using System.Collections.ObjectModel;

namespace KnowledgeHub.Client.ViewModels;

public partial class SourcesViewModel
    : ObservableObject
{
    private readonly SourceApiService _service;

    [ObservableProperty]
    private bool isLoading;

    public ObservableCollection<SourceDto>
        Sources
    {
        get;
    }
    = [];

    public SourcesViewModel(
        SourceApiService service)
    {
        _service = service;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;

        Sources.Clear();

        var items =
            await _service.GetSourcesAsync();

        foreach (var item in items)
        {
            Sources.Add(item);
        }

        IsLoading = false;
    }
}