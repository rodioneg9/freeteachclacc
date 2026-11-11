using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FreeTeachClass.Application.Ai;
using FreeTeachClass.Domain.Enums;

namespace FreeTeachClass.Infrastructure.Storage;

/// <summary>
/// Per-provider AI API keys encrypted with Windows DPAPI (current user).
/// Mirrors the OAuth token store: the file never contains a readable key and
/// corrupted or foreign-account data is treated as "no key".
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiAiKeyStore : IAiKeyStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("FreeTeachClass.AiKeyStore.v1");

    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public DpapiAiKeyStore()
        : this(AppDirectories.AiKeysFile)
    {
    }

    public DpapiAiKeyStore(string filePath)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Хранилище ключей AI использует Windows DPAPI.");
        }

        _filePath = filePath;
    }

    public async Task<bool> HasKeyAsync(AiProviderKind provider, CancellationToken cancellationToken) =>
        !string.IsNullOrEmpty(await GetKeyAsync(provider, cancellationToken).ConfigureAwait(false));

    public async Task<string?> GetKeyAsync(AiProviderKind provider, CancellationToken cancellationToken)
    {
        var keys = await LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!keys.TryGetValue(Key(provider), out var protectedValue))
        {
            return null;
        }

        try
        {
            return Unprotect(protectedValue);
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public Task SaveKeyAsync(AiProviderKind provider, string apiKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API-ключ не может быть пустым.", nameof(apiKey));
        }

        return MutateAsync(
            keys =>
            {
                keys[Key(provider)] = Protect(apiKey.Trim());
                return true;
            },
            cancellationToken);
    }

    public Task DeleteKeyAsync(AiProviderKind provider, CancellationToken cancellationToken) =>
        MutateAsync(keys => keys.Remove(Key(provider)), cancellationToken);

    private static string Key(AiProviderKind provider) => provider.ToString();

    private async Task<Dictionary<string, string>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        string raw;
        try
        {
            raw = await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);
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

    private async Task MutateAsync(
        Func<Dictionary<string, string>, bool> mutate,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var keys = await LoadAsync(cancellationToken).ConfigureAwait(false);
            if (!mutate(keys))
            {
                return;
            }

            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var payload = JsonSerializer.Serialize(keys);
            var tempPath = _filePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, Protect(payload), cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, _filePath, overwrite: true);
        }
        finally
        {
            _gate.Release();
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
