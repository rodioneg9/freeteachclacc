using FreeTeachClass.Domain.Enums;
using FreeTeachClass.Infrastructure.Storage;

namespace FreeTeachClass.Infrastructure.Tests;

/// <summary>
/// DPAPI-backed AI key storage: keys must survive a save/read cycle, must be
/// isolated per provider and must never appear as plain text on disk.
/// Windows-only (DPAPI), so every test guards itself.
/// </summary>
public sealed class DpapiAiKeyStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "FreeTeachClass.AiKeyTests",
        Guid.NewGuid().ToString("N"));

    private string KeysFilePath => Path.Combine(_directory, "ai-keys.dat");

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task SaveReadDelete_RoundTripsPerProvider()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var store = new DpapiAiKeyStore(KeysFilePath);

        Assert.False(await store.HasKeyAsync(AiProviderKind.OpenAi, CancellationToken.None));

        await store.SaveKeyAsync(AiProviderKind.OpenAi, "sk-openai-secret", CancellationToken.None);
        await store.SaveKeyAsync(AiProviderKind.Gemini, "gemini-secret", CancellationToken.None);

        Assert.True(await store.HasKeyAsync(AiProviderKind.OpenAi, CancellationToken.None));
        Assert.Equal("sk-openai-secret", await store.GetKeyAsync(AiProviderKind.OpenAi, CancellationToken.None));
        Assert.Equal("gemini-secret", await store.GetKeyAsync(AiProviderKind.Gemini, CancellationToken.None));
        Assert.Null(await store.GetKeyAsync(AiProviderKind.Anthropic, CancellationToken.None));

        await store.DeleteKeyAsync(AiProviderKind.OpenAi, CancellationToken.None);

        Assert.False(await store.HasKeyAsync(AiProviderKind.OpenAi, CancellationToken.None));
        Assert.True(await store.HasKeyAsync(AiProviderKind.Gemini, CancellationToken.None));
    }

    [Fact]
    public async Task KeyFile_DoesNotContainPlainTextSecrets()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var store = new DpapiAiKeyStore(KeysFilePath);
        await store.SaveKeyAsync(AiProviderKind.OpenAi, "sk-very-secret-value", CancellationToken.None);

        Assert.True(File.Exists(KeysFilePath));
        var raw = await File.ReadAllTextAsync(KeysFilePath, CancellationToken.None);
        Assert.DoesNotContain("sk-very-secret-value", raw);
    }

    [Fact]
    public async Task CorruptedFile_ReadsAsNoKeys()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(KeysFilePath, "not-a-dpapi-payload", CancellationToken.None);

        var store = new DpapiAiKeyStore(KeysFilePath);

        Assert.False(await store.HasKeyAsync(AiProviderKind.OpenAi, CancellationToken.None));
        Assert.Null(await store.GetKeyAsync(AiProviderKind.OpenAi, CancellationToken.None));
    }
}
