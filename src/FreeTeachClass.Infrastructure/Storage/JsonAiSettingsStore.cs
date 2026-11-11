using System.Text.Json;
using System.Text.Json.Serialization;
using FreeTeachClass.Application.Ai;
using FreeTeachClass.Domain.Enums;

namespace FreeTeachClass.Infrastructure.Storage;

/// <summary>
/// Stores non-secret AI preferences in %LOCALAPPDATA%\FreeTeachClass\ai-settings.json.
/// API keys are never written to this file.
/// </summary>
public sealed class JsonAiSettingsStore : IAiSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly AiSettings Defaults = new(
        AiProviderKind.OpenAi,
        "gpt-4.1-mini",
        BaseUrl: null);

    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonAiSettingsStore()
        : this(AppDirectories.AiSettingsFile)
    {
    }

    public JsonAiSettingsStore(string filePath)
    {
        _filePath = filePath;
    }

    public async Task<AiSettings> LoadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_filePath))
            {
                return Defaults;
            }

            try
            {
                await using var stream = File.OpenRead(_filePath);
                var settings = await JsonSerializer
                    .DeserializeAsync<AiSettings>(stream, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
                return settings ?? Defaults;
            }
            catch (JsonException)
            {
                return Defaults;
            }
            catch (IOException)
            {
                return Defaults;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(AiSettings settings, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = _filePath + ".tmp";
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer
                    .SerializeAsync(stream, settings, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
            }

            File.Move(tempPath, _filePath, overwrite: true);
        }
        finally
        {
            _gate.Release();
        }
    }
}
