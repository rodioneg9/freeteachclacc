using FreeTeachClass.Application.Ai;
using FreeTeachClass.Domain.Enums;
using FreeTeachClass.Infrastructure.Storage;

namespace FreeTeachClass.Infrastructure.Tests;

public sealed class JsonAiSettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "FreeTeachClass.AiSettingsTests",
        Guid.NewGuid().ToString("N"));

    private string SettingsFilePath => Path.Combine(_directory, "ai-settings.json");

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
    public async Task MissingFile_ReturnsDefaults()
    {
        var store = new JsonAiSettingsStore(SettingsFilePath);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(AiProviderKind.OpenAi, settings.Provider);
        Assert.Equal("gpt-4.1-mini", settings.Model);
        Assert.Null(settings.BaseUrl);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsSettings()
    {
        var store = new JsonAiSettingsStore(SettingsFilePath);
        var expected = new AiSettings(
            AiProviderKind.Anthropic,
            "claude-sonnet-4-5",
            BaseUrl: null,
            Temperature: 0.3,
            MaxTokens: 4096);

        await store.SaveAsync(expected, CancellationToken.None);
        var actual = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(expected, actual);
        Assert.DoesNotContain("key", await File.ReadAllTextAsync(SettingsFilePath, CancellationToken.None));
    }

    [Fact]
    public async Task CorruptedFile_ReturnsDefaults()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(SettingsFilePath, "{ definitely not json", CancellationToken.None);

        var store = new JsonAiSettingsStore(SettingsFilePath);

        Assert.Equal("gpt-4.1-mini", (await store.LoadAsync(CancellationToken.None)).Model);
    }
}
