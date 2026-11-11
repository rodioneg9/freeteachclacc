using System.Text.Json;

namespace FreeTeachClass.Infrastructure.Storage;

/// <summary>
/// Remembers which OAuth scope set the stored token was issued for. When the
/// application starts requesting new scopes, the old token must be discarded so
/// the user sees the consent screen again (Google does not upgrade a token
/// silently).
/// </summary>
public sealed class AuthStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _filePath;

    public AuthStateStore()
        : this(AppDirectories.AuthStateFile)
    {
    }

    public AuthStateStore(string filePath)
    {
        _filePath = filePath;
    }

    public bool IsCurrent(int scopeVersion)
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return false;
            }

            var state = JsonSerializer.Deserialize<AuthState>(File.ReadAllText(_filePath), SerializerOptions);
            return state?.ScopeVersion == scopeVersion;
        }
        catch (IOException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public void MarkCurrent(int scopeVersion)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                _filePath,
                JsonSerializer.Serialize(new AuthState(scopeVersion), SerializerOptions));
        }
        catch (IOException)
        {
            // Best effort: at worst the next launch asks for consent again.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record AuthState(int ScopeVersion);
}
