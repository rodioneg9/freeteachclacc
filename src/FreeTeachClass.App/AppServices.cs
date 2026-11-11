using FreeTeachClass.Application;
using FreeTeachClass.Infrastructure;
using FreeTeachClass.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FreeTeachClass.App;

/// <summary>
/// Composition root of the Windows client. All services live in a DI
/// container; pages/view models never talk to Google directly.
/// </summary>
public sealed class AppServices
{
    public AppServices()
    {
        AppDirectories.EnsureRootExists();

        var services = new ServiceCollection();
        services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Information);
            logging.AddProvider(new FileLoggerProvider(Path.Combine(AppDirectories.Root, "logs")));
        });

        services.AddFreeTeachClassApplication();
        services.AddFreeTeachClassInfrastructure();

        services.AddSingleton<ThemeService>();
        services.AddSingleton<MainWindow>();
        services.AddSingleton<INavigator>(sp => sp.GetRequiredService<MainWindow>());
        RegisterPagesAndViewModels(services);

        Provider = services.BuildServiceProvider();
    }

    public IServiceProvider Provider { get; }

    public static AppServices Current { get; } = new();

    private static void RegisterPagesAndViewModels(IServiceCollection services)
    {
        services.AddSingleton<ViewModels.WelcomeViewModel>();
        services.AddSingleton<ViewModels.DashboardViewModel>();
        services.AddSingleton<ViewModels.CoursesViewModel>();
        services.AddSingleton<ViewModels.AssignmentsViewModel>();
        services.AddSingleton<ViewModels.AssignmentDetailViewModel>();
        services.AddSingleton<ViewModels.AssistantViewModel>();
        services.AddSingleton<ViewModels.SettingsViewModel>();

        services.AddSingleton<Views.WelcomePage>();
        services.AddSingleton<Views.DashboardPage>();
        services.AddSingleton<Views.CoursesPage>();
        services.AddSingleton<Views.AssignmentsPage>();
        services.AddSingleton<Views.AssignmentDetailPage>();
        services.AddSingleton<Views.PlanPage>();
        services.AddSingleton<Views.AssistantPage>();
        services.AddSingleton<Views.AchievementsPage>();
        services.AddSingleton<Views.SettingsPage>();
    }
}
