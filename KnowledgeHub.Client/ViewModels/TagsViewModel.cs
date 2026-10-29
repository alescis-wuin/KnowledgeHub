using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnowledgeHub.Client.Models;
using KnowledgeHub.Client.Services;
using System.Collections.ObjectModel;

namespace KnowledgeHub.Client.ViewModels;

public partial class TagsViewModel
    : ObservableObject
{
    private readonly TagApiService _service;

    [ObservableProperty]
    private bool isLoading;

    public ObservableCollection<TagDto>
        Tags
    {
        get;
    }
    = [];

    public TagsViewModel(
        TagApiService service)
    {
        _service = service;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;

        Tags.Clear();

        var items =
            await _service.GetTagsAsync();

        foreach (var item in items)
        {
            Tags.Add(item);
        }

        IsLoading = false;
    }
}