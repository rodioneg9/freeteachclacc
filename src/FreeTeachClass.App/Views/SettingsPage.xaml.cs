using FreeTeachClass.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FreeTeachClass.App.Views;

public sealed partial class SettingsPage : Page
{
    private readonly PageLoader _loader;

    public SettingsViewModel ViewModel { get; }

    public SettingsPage(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        _loader = new PageLoader(this, token => ViewModel.LoadAsync(token));
    }

    private void OnAiKeyPasswordChanged(object sender, RoutedEventArgs e) =>
        ViewModel.AiKeyInput = ((PasswordBox)sender).Password;

    private async void OnSaveAiKeyClick(object sender, RoutedEventArgs e)
    {
        ViewModel.AiKeyInput = AiKeyBox.Password;
        await ViewModel.SaveAiKeyAsync();
        if (string.IsNullOrEmpty(ViewModel.AiKeyInput))
        {
            AiKeyBox.Password = string.Empty;
        }
    }

    private async void OnTestAiKeyClick(object sender, RoutedEventArgs e)
    {
        ViewModel.AiKeyInput = AiKeyBox.Password;
        await ViewModel.TestAiKeyAsync();
    }

    private async void OnDeleteAiKeyClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.DeleteAiKeyAsync();
        AiKeyBox.Password = string.Empty;
    }
}
