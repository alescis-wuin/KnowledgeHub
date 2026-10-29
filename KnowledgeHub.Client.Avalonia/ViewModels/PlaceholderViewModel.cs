
namespace KnowledgeHub.Client.Avalonia.ViewModels;

public sealed class PlaceholderViewModel : ViewModelBase
{
    public string Title { get; }

    public string Description { get; } =
        "Cet écran sera intégré lors d'un prochain jalon.";

    public PlaceholderViewModel(string title)
    {
        Title = title;
    }
}
