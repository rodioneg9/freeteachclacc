using FreeTeachClass.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace FreeTeachClass.App.Views;

public sealed partial class CoursesPage : Page
{
    private readonly PageLoader _loader;

    public CoursesViewModel ViewModel { get; }

    public CoursesPage(CoursesViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        _loader = new PageLoader(this, token => ViewModel.LoadAsync(token));
    }
}
