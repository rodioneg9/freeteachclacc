using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FreeTeachClass.Domain.Abstractions;
using FreeTeachClass.Infrastructure.Google;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Util.Store;

namespace FreeTeachClass.Infrastructure.Storage;

/// <summary>
/// Stores OAuth tokens encrypted with Windows DPAPI (per current user) —
/// never as plain text. Implements both the domain-level abstraction and the
/// <see cref="IDataStore"/> expected by the Google API client library, so the
/// library keeps tokens here too (including auto-refreshed ones).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiTokenStore : ISecureTokenStore, IDataStore
{
    /// <summary>
    /// Must equal the user id the OAuth gateway passes to Google's broker —
    /// that is the key under which the flow writes the token.
    /// </summary>
    private const string TokenKey = GoogleAuthGateway.FlowUser;

    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("FreeTeachClass.TokenStore.v1");

    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public DpapiTokenStore()
        : this(AppDirectories.TokenFile)
    {
    }

    public DpapiTokenStore(string filePath)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Хранилище токенов использует Windows DPAPI.");
        }

        _filePath = filePath;
    }

    // ---- ISecureTokenStore (domain level, cancellation-aware) ----

    public async Task<bool> HasTokenAsync(CancellationToken cancellationToken)
    {
        var json = await LoadAsync(TokenKey).ConfigureAwait(false);
        if (string.IsNullOrEmpty(json))
        {
            return false;
        }

        var token = DeserializeToken(json);
        return token?.RefreshToken?.Length > 0;
    }

    public Task ClearAsync(CancellationToken cancellationToken) =>
        MutateAsync(dictionary =>
        {
            dictionary.Clear();
            return true;
        });

    // ---- IDataStore (Google client library) ----

    public async Task<T> GetAsync<T>(string key)
    {
        var json = await LoadAsync(key).ConfigureAwait(false);
        if (string.IsNullOrEmpty(json))
        {
            return default!;
        }

        if (typeof(T) == typeof(TokenResponse))
        {
            var token = DeserializeToken(json);
            return token is null ? default! : (T)(object)token;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json)!;
        }
        catch (JsonException)
        {
            return default!;
        }
    }

    public Task StoreAsync<T>(string key, T value) =>
        MutateAsync(dictionary =>
        {
            dictionary[key] = Protect(SerializeValue(value));
            return true;
        });

    public Task DeleteAsync<T>(string key) =>
        MutateAsync(dictionary => dictionary.Remove(key));

    public Task ClearAsync() => ClearAsync(CancellationToken.None);

    // ---- internals ----

    private static string SerializeValue<T>(T value) => value switch
    {
        null => throw new ArgumentNullException(nameof(value)),
        // Keep the library's own JSON shape for its TokenResponse type.
        TokenResponse token => Newtonsoft.Json.JsonConvert.SerializeObject(token),
        _ => JsonSerializer.Serialize(value),
    };

    private static TokenResponse? DeserializeToken(string json)
    {
        try
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<TokenResponse>(json);
        }
        catch (Newtonsoft.Json.JsonException)
        {
            return null;
        }
    }

    private async Task<string?> LoadAsync(string key)
    {
        var dictionary = await LoadDictionaryAsync().ConfigureAwait(false);
        if (!dictionary.TryGetValue(key, out var protectedValue))
        {
            return null;
        }

        try
        {
            return Unprotect(protectedValue);
        }
        catch (CryptographicException)
        {
            // Stored under another Windows account or corrupted — treat as absent.
            return null;
        }
    }

    private async Task MutateAsync(Func<Dictionary<string, string>, bool> mutate)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var dictionary = await LoadDictionaryAsync().ConfigureAwait(false);
            if (!mutate(dictionary))
            {
                return;
            }

            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var payload = JsonSerializer.Serialize(dictionary);
            await File.WriteAllTextAsync(_filePath, Protect(payload)).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Dictionary<string, string>> LoadDictionaryAsync()
    {
        if (!File.Exists(_filePath))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        string raw;
        try
        {
            raw = await File.ReadAllTextAsync(_filePath).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            var json = Unprotect(raw);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (CryptographicException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (FormatException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private static string Protect(string plainText) =>
        Convert.ToBase64String(ProtectedData.Protect(
            Encoding.UTF8.GetBytes(plainText),
            Entropy,
            DataProtectionScope.CurrentUser));

    private static string Unprotect(string protectedText) =>
        Encoding.UTF8.GetString(ProtectedData.Unprotect(
            Convert.FromBase64String(protectedText),
            Entropy,
            DataProtectionScope.CurrentUser));
}
