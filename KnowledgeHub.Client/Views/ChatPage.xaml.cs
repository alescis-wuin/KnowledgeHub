using KnowledgeHub.Client.ViewModels;

namespace KnowledgeHub.Client.Views;

public partial class ChatPage : ContentPage
{
    public ChatPage(
        ChatViewModel viewModel)
    {
        InitializeComponent();

        BindingContext = viewModel;
    }
}