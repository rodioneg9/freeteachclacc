using System.Collections.Specialized;
using FreeTeachClass.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FreeTeachClass.App.Views;

/// <summary>AI chat with optional assignment context.</summary>
public sealed partial class AssistantPage : Page
{
    private readonly PageLoader _loader;

    public AssistantViewModel ViewModel { get; }

    public AssistantPage(AssistantViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        _loader = new PageLoader(this, token => ViewModel.LoadAsync(token));
        ViewModel.Messages.CollectionChanged += OnMessagesChanged;
    }

    private void OnInputTextChanged(object sender, TextChangedEventArgs e) =>
        ViewModel.Input = InputBox.Text;

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // The chat is newest-at-bottom; scroll after XAML has created the row.
        DispatcherQueue.TryEnqueue(() =>
            MessagesScroll.ChangeView(null, MessagesScroll.ScrollableHeight, null));
    }
}
