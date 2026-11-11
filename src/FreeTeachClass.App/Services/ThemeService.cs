using FreeTeachClass.Infrastructure.Storage;
using Microsoft.UI.Xaml;

namespace FreeTeachClass.App;

public enum AppTheme
{
    System,
    Light,
    Dark,
}

/// <summary>
/// Applies and persists the requested theme (dark / light / follow system).
/// </summary>
public sealed class ThemeService
{
    private const string ThemeSettingKey = "theme";

    private AppTheme _current = AppTheme.System;

    public AppTheme Current
    {
        get => _current;
        set
        {
            _current = value;
            Persist(value);
        }
    }

    public void ApplyTo(FrameworkElement element)
    {
        element.RequestedTheme = _current switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
    }

    public void Load()
    {
        var stored = UiSettingsStore.Read();
        if (stored is not null
            && Enum.TryParse<AppTheme>(stored, ignoreCase: true, out var parsed))
        {
            _current = parsed;
        }
    }

    private static void Persist(AppTheme theme)
    {
        UiSettingsStore.Write(new Dictionary<string, string>
        {
            ["theme"] = theme.ToString(),
        });
    }
}
