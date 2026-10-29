using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnowledgeHub.Client.Models;
using KnowledgeHub.Client.Services;
using System.Collections.ObjectModel;

namespace KnowledgeHub.Client.ViewModels;

public partial class CategoriesViewModel
    : ObservableObject
{
    private readonly CategoryApiService _service;

    [ObservableProperty]
    private bool isLoading;

    public ObservableCollection<CategoryDto>
        Categories
    {
        get;
    }
    = [];

    public CategoriesViewModel(
        CategoryApiService service)
    {
        _service = service;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;

        Categories.Clear();

        var items =
            await _service.GetCategoriesAsync();

        foreach (var item in items)
        {
            Categories.Add(item);
        }

        IsLoading = false;
    }
}