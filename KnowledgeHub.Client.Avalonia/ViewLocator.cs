
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using KnowledgeHub.Client.Avalonia.ViewModels;
using KnowledgeHub.Client.Avalonia.Views;

namespace KnowledgeHub.Client.Avalonia;

/// <summary>
/// Resolves view models to their corresponding views
/// without runtime reflection.
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    public Control? Build(object? data)
    {
        return data switch
        {
            DashboardViewModel vm => new DashboardView
            {
                DataContext = vm
            },

            DocumentsViewModel vm => new DocumentsView
            {
                DataContext = vm
            },

            ReferenceListViewModel vm => new ReferenceListView
            {
                DataContext = vm
            },

            ChatViewModel vm => new ChatView
            {
                DataContext = vm
            },

            PlaceholderViewModel vm => new PlaceholderView
            {
                DataContext = vm
            },

            null => null,

            _ => new TextBlock
            {
                Text =
                    $"Aucune vue enregistrée pour " +
                    $"{data.GetType().Name}"
            }
        };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
