using System.Text.Json;
using FreeTeachClass.Infrastructure.Storage;

namespace FreeTeachClass.App;

/// <summary>
/// Tiny key-value JSON store for UI preferences (theme, window state).
/// Deliberately separate from the Classroom data cache.
/// </summary>
public static class UiSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public static string? Read()
    {
        try
        {
            if (!File.Exists(AppDirectories.UiSettingsFile))
            {
                return null;
            }

            var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(AppDirectories.UiSettingsFile));
            return settings is not null && settings.TryGetValue("theme", out var theme) ? theme : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static void Write(Dictionary<string, string> values)
    {
        try
        {
            AppDirectories.EnsureRootExists();
            File.WriteAllText(
                AppDirectories.UiSettingsFile,
                JsonSerializer.Serialize(values, SerializerOptions));
        }
        catch (IOException)
        {
            // Preferences are best effort.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
