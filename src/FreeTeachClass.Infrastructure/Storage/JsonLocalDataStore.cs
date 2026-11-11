using System.Text.Json;
using System.Text.Json.Serialization;
using FreeTeachClass.Domain.Abstractions;
using FreeTeachClass.Domain.Entities;

namespace FreeTeachClass.Infrastructure.Storage;

/// <summary>
/// Local cache of synced Classroom data as JSON files under
/// %LOCALAPPDATA%\FreeTeachClass\cache. Contains study data (course titles,
/// assignments) — no credentials or tokens are ever written here.
/// </summary>
public sealed class JsonLocalDataStore : ILocalDataStore
{
    private const string UserFileName = "user.json";
    private const string CoursesFileName = "courses.json";
    private const string AssignmentsFileName = "assignments.json";
    private const string MetaFileName = "meta.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _cacheDirectory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonLocalDataStore()
        : this(AppDirectories.CacheDirectory)
    {
    }

    public JsonLocalDataStore(string cacheDirectory)
    {
        _cacheDirectory = cacheDirectory;
    }

    public Task<UserProfile?> LoadUserAsync(CancellationToken cancellationToken) =>
        ReadAsync<UserProfile>(UserFileName, cancellationToken);

    public Task SaveUserAsync(UserProfile user, CancellationToken cancellationToken) =>
        WriteAsync(UserFileName, user, cancellationToken);

    public async Task<IReadOnlyList<Course>> LoadCoursesAsync(CancellationToken cancellationToken) =>
        await ReadAsync<List<Course>>(CoursesFileName, cancellationToken).ConfigureAwait(false)
        ?? [];

    public Task SaveCoursesAsync(IReadOnlyList<Course> courses, CancellationToken cancellationToken) =>
        WriteAsync(CoursesFileName, courses.ToList(), cancellationToken);

    public async Task<IReadOnlyList<Assignment>> LoadAssignmentsAsync(CancellationToken cancellationToken) =>
        await ReadAsync<List<Assignment>>(AssignmentsFileName, cancellationToken).ConfigureAwait(false)
        ?? [];

    public Task SaveAssignmentsAsync(IReadOnlyList<Assignment> assignments, CancellationToken cancellationToken) =>
        WriteAsync(AssignmentsFileName, assignments.ToList(), cancellationToken);

    public async Task<DateTimeOffset?> LoadLastSyncUtcAsync(CancellationToken cancellationToken)
    {
        var meta = await ReadAsync<SyncMeta>(MetaFileName, cancellationToken).ConfigureAwait(false);
        return meta?.LastSyncUtc;
    }

    public Task SaveLastSyncUtcAsync(DateTimeOffset syncedAtUtc, CancellationToken cancellationToken) =>
        WriteAsync(MetaFileName, new SyncMeta(syncedAtUtc), cancellationToken);

    public async Task ClearAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Directory.Exists(_cacheDirectory))
            {
                Directory.Delete(_cacheDirectory, recursive: true);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<T?> ReadAsync<T>(string fileName, CancellationToken cancellationToken)
        where T : class
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var path = Path.Combine(_cacheDirectory, fileName);
            if (!File.Exists(path))
            {
                return null;
            }

            await using var stream = File.OpenRead(path);
            return await JsonSerializer
                .DeserializeAsync<T>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            // A corrupted cache file behaves like an empty cache; sync will rewrite it.
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task WriteAsync<T>(string fileName, T value, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_cacheDirectory);
            var path = Path.Combine(_cacheDirectory, fileName);

            await using var stream = File.Create(path);
            await JsonSerializer
                .SerializeAsync(stream, value, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private sealed record SyncMeta(DateTimeOffset? LastSyncUtc);
}
