using FreeTeachClass.Domain.Entities;
using FreeTeachClass.Domain.Enums;
using FreeTeachClass.Infrastructure.Storage;

namespace FreeTeachClass.Infrastructure.Tests;

/// <summary>
/// Local JSON cache: round trips, empty cache behaviour and recovery from a
/// corrupted file. Uses a throw-away directory, never the real app cache.
/// </summary>
public sealed class JsonLocalDataStoreTests : IDisposable
{
    private readonly string _cacheDirectory = Path.Combine(
        Path.GetTempPath(),
        "FreeTeachClass.Tests",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_cacheDirectory))
            {
                Directory.Delete(_cacheDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            // A leftover temp directory must not fail the test run.
        }
    }

    [Fact]
    public async Task RoundTripsCoursesAssignmentsUserAndSyncTime()
    {
        var writtenAt = new DateTimeOffset(2026, 10, 18, 9, 30, 0, TimeSpan.Zero);
        var dueAt = new DateTimeOffset(2026, 10, 20, 23, 59, 59, TimeSpan.FromHours(3));

        var courses = new List<Course>
        {
            new("c1", "Математика", "10А", "214", "Математика — 10 класс",
                CourseState.Active, writtenAt, writtenAt),
        };
        var assignments = new List<Assignment>
        {
            new("a1", "c1", "Домашняя работа", "стр. 12–15", dueAt,
                AssignmentState.Published, AssignmentSource.GoogleClassroom,
                AssignmentWorkType.Assignment, writtenAt, writtenAt, 25d, "topic-1",
                [new CourseMaterial("c1", "a1", CourseMaterialKind.Link, "Конспект",
                    "https://example.com/notes", null, null)]),
        };
        var user = new UserProfile("sub-1", "Иван Петров", "ivan@example.com", null);

        var writer = new JsonLocalDataStore(_cacheDirectory);
        await writer.SaveCoursesAsync(courses, CancellationToken.None);
        await writer.SaveAssignmentsAsync(assignments, CancellationToken.None);
        await writer.SaveUserAsync(user, CancellationToken.None);
        await writer.SaveLastSyncUtcAsync(writtenAt, CancellationToken.None);

        // A second instance (fresh process, same cache directory) must see it all.
        var reader = new JsonLocalDataStore(_cacheDirectory);

        var loadedCourses = await reader.LoadCoursesAsync(CancellationToken.None);
        var loadedAssignments = await reader.LoadAssignmentsAsync(CancellationToken.None);
        var loadedUser = await reader.LoadUserAsync(CancellationToken.None);
        var loadedSync = await reader.LoadLastSyncUtcAsync(CancellationToken.None);

        var course = Assert.Single(loadedCourses);
        Assert.Equal(courses[0], course);

        var assignment = Assert.Single(loadedAssignments);
        Assert.Equal("a1", assignment.Id);
        Assert.Equal("c1", assignment.CourseId);
        Assert.Equal("Домашняя работа", assignment.Title);
        Assert.Equal("стр. 12–15", assignment.Description);
        Assert.Equal(dueAt, assignment.DueAt);
        Assert.Equal(AssignmentState.Published, assignment.State);
        Assert.Equal(AssignmentSource.GoogleClassroom, assignment.Source);
        Assert.Equal(AssignmentWorkType.Assignment, assignment.WorkType);
        Assert.Equal(writtenAt, assignment.CreationTime);
        Assert.Equal(25d, assignment.MaxPoints);
        Assert.Equal("topic-1", assignment.TopicId);

        var material = Assert.Single(assignment.Materials);
        Assert.Equal(CourseMaterialKind.Link, material.Kind);
        Assert.Equal("Конспект", material.Title);
        Assert.Equal("https://example.com/notes", material.Url);

        Assert.NotNull(loadedUser);
        Assert.Equal("Иван Петров", loadedUser!.Name);
        Assert.Equal("ivan@example.com", loadedUser.Email);

        Assert.Equal(writtenAt, loadedSync);
    }

    [Fact]
    public async Task MissingFiles_BehaveLikeAnEmptyCache()
    {
        var store = new JsonLocalDataStore(_cacheDirectory);

        Assert.Null(await store.LoadUserAsync(CancellationToken.None));
        Assert.Null(await store.LoadLastSyncUtcAsync(CancellationToken.None));
        Assert.Empty(await store.LoadCoursesAsync(CancellationToken.None));
        Assert.Empty(await store.LoadAssignmentsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CorruptedCacheFile_IsTreatedAsEmptyAndCanBeRewritten()
    {
        Directory.CreateDirectory(_cacheDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(_cacheDirectory, "courses.json"),
            "{ this is not json",
            CancellationToken.None);

        var store = new JsonLocalDataStore(_cacheDirectory);
        Assert.Empty(await store.LoadCoursesAsync(CancellationToken.None));

        await store.SaveCoursesAsync(
            [new Course("c1", "Физика", null, null, null, CourseState.Active,
                DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch)],
            CancellationToken.None);

        var reloaded = await store.LoadCoursesAsync(CancellationToken.None);
        Assert.Equal("Физика", Assert.Single(reloaded).Name);
    }

    [Fact]
    public async Task ClearAsync_RemovesTheWholeCacheDirectory()
    {
        var store = new JsonLocalDataStore(_cacheDirectory);
        await store.SaveLastSyncUtcAsync(DateTimeOffset.UtcNow, CancellationToken.None);
        Assert.True(Directory.Exists(_cacheDirectory));

        await store.ClearAsync(CancellationToken.None);

        Assert.False(Directory.Exists(_cacheDirectory));
        Assert.Null(await store.LoadLastSyncUtcAsync(CancellationToken.None));
    }
}
