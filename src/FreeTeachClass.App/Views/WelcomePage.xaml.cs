using FreeTeachClass.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace FreeTeachClass.App.Views;

public sealed partial class WelcomePage : Page
{
    public WelcomeViewModel ViewModel { get; }

    public WelcomePage(WelcomeViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}
