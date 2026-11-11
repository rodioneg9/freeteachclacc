using FreeTeachClass.Domain.Abstractions;
using FreeTeachClass.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace FreeTeachClass.Application.Services;

public interface IClassroomSyncService
{
    /// <summary>
    /// Pulls courses and assignments from Google Classroom and writes them to
    /// the local cache. Failing to sync a single course does not abort the run.
    /// </summary>
    Task<SyncResult> SyncAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Owns the sync pipeline: Google API → Application → local storage → UI.
/// UI never calls Google directly; it only reads the local cache.
/// </summary>
public sealed class ClassroomSyncService : IClassroomSyncService
{
    private readonly IClassroomDataSource _dataSource;
    private readonly ILocalDataStore _localDataStore;
    private readonly ILogger<ClassroomSyncService> _logger;

    public ClassroomSyncService(
        IClassroomDataSource dataSource,
        ILocalDataStore localDataStore,
        ILogger<ClassroomSyncService> logger)
    {
        _dataSource = dataSource;
        _localDataStore = localDataStore;
        _logger = logger;
    }

    public async Task<SyncResult> SyncAsync(CancellationToken cancellationToken)
    {
        var syncedAt = DateTimeOffset.UtcNow;
        var courses = await _dataSource.GetCoursesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Fetched {Count} courses", courses.Count);

        var assignments = new List<Assignment>();
        var failedCourses = 0;

        foreach (var course in courses)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var courseAssignments = await _dataSource
                    .GetAssignmentsAsync(course.Id, cancellationToken)
                    .ConfigureAwait(false);
                assignments.AddRange(courseAssignments);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failedCourses++;
                _logger.LogWarning(ex, "Failed to fetch coursework for course {CourseId}", course.Id);
            }
        }

        await _localDataStore.SaveCoursesAsync(courses, cancellationToken).ConfigureAwait(false);
        await _localDataStore.SaveAssignmentsAsync(assignments, cancellationToken).ConfigureAwait(false);
        await _localDataStore.SaveLastSyncUtcAsync(syncedAt, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Sync finished: {Assignments} assignments, {Failed} courses failed",
            assignments.Count,
            failedCourses);

        return new SyncResult(courses.Count - failedCourses, assignments.Count, failedCourses, syncedAt);
    }
}
