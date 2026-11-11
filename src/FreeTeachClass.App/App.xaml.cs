using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using XamlApplication = Microsoft.UI.Xaml.Application;

namespace FreeTeachClass.App;

public sealed partial class App : XamlApplication
{
    public App()
    {
        CrashLog.Attach();
        UnhandledException += OnUnhandledException;
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var window = AppServices.Current.Provider.GetRequiredService<MainWindow>();
        window.Activate();
        _ = window.StartAsync();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        CrashLog.Write("XAML", e.Exception, isTerminating: !e.Handled);

        // Swallowing the exception keeps the app alive (WinUI would otherwise
        // terminate the process); the page that failed already surfaced its own
        // error state and the failure is on disk for diagnostics.
        e.Handled = true;
    }
}
