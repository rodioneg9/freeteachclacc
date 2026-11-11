using FreeTeachClass.Application.Services;
using FreeTeachClass.App.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FreeTeachClass.App;

/// <summary>
/// Application shell: sidebar navigation + page hosting. Pages come from the
/// DI container, so view models are injected, never resolved ad hoc.
/// </summary>
public sealed partial class MainWindow : Window, INavigator
{
    private readonly IServiceProvider _services;
    private readonly ThemeService _themeService;
    private readonly ILogger<MainWindow> _logger;

    private bool _suppressSelectionHandling;

    public MainWindow(
        IServiceProvider services,
        ThemeService themeService,
        ILogger<MainWindow> logger)
    {
        InitializeComponent();

        _services = services;
        _themeService = themeService;
        _logger = logger;

        _themeService.ApplyTo(NavView);
    }

    /// <summary>
    /// Startup: silent session restore when a refresh token exists,
    /// otherwise the welcome screen.
    /// </summary>
    public async Task StartAsync()
    {
        try
        {
            var authentication = _services.GetRequiredService<IAuthenticationService>();
            if (await authentication.IsSignedInAsync(CancellationToken.None))
            {
                var profile = await authentication.TryRestoreSessionAsync(CancellationToken.None);
                if (profile is not null)
                {
                    Navigate(PageKey.Dashboard);
                    return;
                }

                _logger.LogInformation("Stored session could not be restored; showing welcome screen");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Silent session restore failed; showing welcome screen");
        }

        Navigate(PageKey.Welcome);
    }

    public void Navigate(PageKey key)
    {
        var page = Resolve(key);
        RootGrid.Children.Clear();
        RootGrid.Children.Add(page);

        _suppressSelectionHandling = true;
        try
        {
            var item = FindNavItem(key);
            if (item is not null)
            {
                NavView.SelectedItem = item;
            }
        }
        finally
        {
            _suppressSelectionHandling = false;
        }
    }

    public void RefreshTheme() => _themeService.ApplyTo(NavView);

    private void OnNavSelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (_suppressSelectionHandling || args.SelectedItem is not NavigationViewItem item)
        {
            return;
        }

        if (item.Tag is string tag && Enum.TryParse<PageKey>(tag, ignoreCase: false, out var key))
        {
            Navigate(key);
        }
    }

    private UIElement Resolve(PageKey key) => key switch
    {
        PageKey.Welcome => _services.GetRequiredService<WelcomePage>(),
        PageKey.Dashboard => _services.GetRequiredService<DashboardPage>(),
        PageKey.Courses => _services.GetRequiredService<CoursesPage>(),
        PageKey.Assignments => _services.GetRequiredService<AssignmentsPage>(),
        PageKey.AssignmentDetail => _services.GetRequiredService<AssignmentDetailPage>(),
        PageKey.Plan => _services.GetRequiredService<PlanPage>(),
        PageKey.Assistant => _services.GetRequiredService<AssistantPage>(),
        PageKey.Achievements => _services.GetRequiredService<AchievementsPage>(),
        PageKey.Settings => _services.GetRequiredService<SettingsPage>(),
        _ => throw new ArgumentOutOfRangeException(nameof(key)),
    };

    private NavigationViewItem? FindNavItem(PageKey key)
    {
        var allItems = NavView.MenuItems
            .Concat(NavView.FooterMenuItems)
            .OfType<NavigationViewItem>();

        return allItems.FirstOrDefault(item => item.Tag as string == key.ToString());
    }
}
