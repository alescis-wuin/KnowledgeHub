using KnowledgeHub.Client.Views;

namespace KnowledgeHub.Client;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(
            nameof(DocumentDetailsPage),
            typeof(DocumentDetailsPage));
    }
}