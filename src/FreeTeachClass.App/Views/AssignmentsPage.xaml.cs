using FreeTeachClass.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace FreeTeachClass.App.Views;

public sealed partial class AssignmentsPage : Page
{
    private readonly PageLoader _loader;

    public AssignmentsViewModel ViewModel { get; }

    public AssignmentsPage(AssignmentsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        _loader = new PageLoader(this, token => ViewModel.LoadAsync(token));
    }
}
