using System.Text.Json;
using FreeTeachClass.Infrastructure.Storage;

namespace FreeTeachClass.Infrastructure.Google;

/// <summary>
/// OAuth client configuration for the desktop (installed) app flow.
/// Credentials are resolved from three sources, highest priority first:
/// environment variables, the local config file and the values hardcoded in
/// <see cref="GoogleOAuthDefaults"/> (see README).
/// </summary>
public sealed class GoogleOAuthOptions
{
    public const string ClientIdEnvironmentVariable = "FREETEACHCLASS_GOOGLE_CLIENT_ID";
    public const string ClientSecretEnvironmentVariable = "FREETEACHCLASS_GOOGLE_CLIENT_SECRET";

    public string ClientId { get; init; } = string.Empty;

    /// <summary>
    /// Desktop clients get a "client secret", but it is not confidential
    /// (public clients cannot keep secrets). Optional at runtime.
    /// </summary>
    public string? ClientSecret { get; init; }

    /// <summary>
    /// Load order: environment variables, then %LOCALAPPDATA%\FreeTeachClass\oauth-client.json
    /// (either the Google client shape {"installed": {"client_id": …}} or the flat
    /// {"clientId": …, "clientSecret": …}), then the credentials hardcoded in
    /// <see cref="GoogleOAuthDefaults"/>. Returns false when nothing is configured.
    /// </summary>
    public static bool TryLoad(out GoogleOAuthOptions? options) =>
        TryLoad(out options, AppDirectories.Root, Environment.GetEnvironmentVariable);

    /// <summary>
    /// Testable overload: the config directory and the environment reader are
    /// injected so tests never touch the real user profile.
    /// </summary>
    internal static bool TryLoad(
        out GoogleOAuthOptions? options,
        string appDataRoot,
        Func<string, string?> environmentReader)
    {
        if (TryLoadFromEnvironment(environmentReader, out options))
        {
            return true;
        }

        if (TryLoadFromFile(appDataRoot, out options))
        {
            return true;
        }

        return TryLoadFromBuiltInDefaults(out options);
    }

    private static bool TryLoadFromEnvironment(
        Func<string, string?> environmentReader,
        out GoogleOAuthOptions? options)
    {
        var envClientId = environmentReader(ClientIdEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(envClientId))
        {
            options = new GoogleOAuthOptions
            {
                ClientId = CleanValue(envClientId)!,
                ClientSecret = CleanValue(environmentReader(ClientSecretEnvironmentVariable)),
            };
            return true;
        }

        options = null;
        return false;
    }

    private static bool TryLoadFromFile(string appDataRoot, out GoogleOAuthOptions? options)
    {
        options = null;

        var configFile = Path.Combine(appDataRoot, "oauth-client.json");
        if (!File.Exists(configFile))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(configFile));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            // The file downloaded from Google Cloud Console looks like
            // { "installed": { "client_id": …, "client_secret": … } };
            // the flat { "clientId": …, "clientSecret": … } shape is accepted too.
            var section = document.RootElement;
            foreach (var name in new[] { "installed", "web" })
            {
                if (section.TryGetProperty(name, out var nested) && nested.ValueKind == JsonValueKind.Object)
                {
                    section = nested;
                    break;
                }
            }

            var clientId = CleanValue(ReadString(section, "clientId", "client_id"));
            if (clientId is null)
            {
                return false;
            }

            options = new GoogleOAuthOptions
            {
                ClientId = clientId,
                ClientSecret = CleanValue(ReadString(section, "clientSecret", "client_secret")),
            };
            return true;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Fall through: treat an unreadable config as "not configured".
            options = null;
            return false;
        }
    }

    private static bool TryLoadFromBuiltInDefaults(out GoogleOAuthOptions? options)
    {
        options = null;

        var clientId = CleanValue(GoogleOAuthDefaults.ClientId);
        if (clientId is null)
        {
            return false;
        }

        options = new GoogleOAuthOptions
        {
            ClientId = clientId,
            ClientSecret = CleanValue(GoogleOAuthDefaults.ClientSecret),
        };
        return true;
    }

    private static string? ReadString(JsonElement section, params string[] names)
    {
        foreach (var name in names)
        {
            if (section.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    /// <summary>
    /// Normalises a credential value: trims it, drops surrounding quotes and
    /// rejects empty or placeholder values ("&lt;client-id&gt;", "PASTE_ME", …).
    /// </summary>
    private static string? CleanValue(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.Trim().Trim('"', '\'', '`');
        if (value.Length == 0 || IsPlaceholder(value))
        {
            return null;
        }

        return value;
    }

    private static bool IsPlaceholder(string value) =>
        value.Contains('<') ||
        value.Contains('>') ||
        value.Contains('{') ||
        value.Contains('}') ||
        value.Contains("PASTE", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("YOUR_", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("TODO", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("REPLACE", StringComparison.OrdinalIgnoreCase);
}
