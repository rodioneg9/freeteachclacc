using FreeTeachClass.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace FreeTeachClass.App.Views;

public sealed partial class DashboardPage : Page
{
    private readonly PageLoader _loader;

    public DashboardViewModel ViewModel { get; }

    public DashboardPage(DashboardViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        _loader = new PageLoader(this, token => ViewModel.LoadAsync(token));
    }
}
