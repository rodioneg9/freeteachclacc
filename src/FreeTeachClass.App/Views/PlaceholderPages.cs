using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XamlApplication = Microsoft.UI.Xaml.Application;

namespace FreeTeachClass.App.Views;

/// <summary>
/// Base for the not-implemented-yet sections. UI is composed in code —
/// no XAML boilerplate for one-title pages.
/// </summary>
public class PlaceholderPage : Page
{
    protected PlaceholderPage(string icon, string title, string description)
    {
        var panel = new StackPanel
        {
            MaxWidth = 520,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 12,
        };

        panel.Children.Add(new TextBlock
        {
            Text = icon,
            FontSize = 48,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        panel.Children.Add(new TextBlock
        {
            Text = title,
            Style = (Style)XamlApplication.Current.Resources["TitleTextBlockStyle"],
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        panel.Children.Add(new TextBlock
        {
            Text = description,
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Раздел появится в одной из следующих версий.",
            Opacity = 0.5,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        Content = new ScrollViewer { Content = panel };
    }
}

public sealed class PlanPage : PlaceholderPage
{
    public PlanPage()
        : base("📅", "План", "Здесь будет планировщик учебы: когда что готовить и когда отдохнуть.")
    {
    }
}

public sealed class AchievementsPage : PlaceholderPage
{
    public AchievementsPage()
        : base("🏆", "Достижения", "Здесь будут XP, уровни и достижения за выполненную работу.")
    {
    }
}
