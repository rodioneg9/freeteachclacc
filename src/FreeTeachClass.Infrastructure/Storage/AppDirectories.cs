namespace FreeTeachClass.Infrastructure.Storage;

/// <summary>Well-known local directories under %LOCALAPPDATA%\FreeTeachClass.</summary>
public static class AppDirectories
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FreeTeachClass");

    public static string CacheDirectory => Path.Combine(Root, "cache");

    public static string TokenFile => Path.Combine(Root, "tokens.dat");

    public static string UiSettingsFile => Path.Combine(Root, "ui-settings.json");

    public static string AiSettingsFile => Path.Combine(Root, "ai-settings.json");

    public static string AiKeysFile => Path.Combine(Root, "ai-keys.dat");

    public static string AuthStateFile => Path.Combine(Root, "auth-state.json");

    public static void EnsureRootExists() => Directory.CreateDirectory(Root);
}
