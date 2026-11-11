using FreeTeachClass.Domain.Entities;

namespace FreeTeachClass.Domain.Abstractions;

/// <summary>
/// Local cache of synced Classroom data. Screens read exclusively from here;
/// only the sync service writes to it.
/// </summary>
public interface ILocalDataStore
{
    Task<UserProfile?> LoadUserAsync(CancellationToken cancellationToken);
    Task SaveUserAsync(UserProfile user, CancellationToken cancellationToken);

    Task<IReadOnlyList<Course>> LoadCoursesAsync(CancellationToken cancellationToken);
    Task SaveCoursesAsync(IReadOnlyList<Course> courses, CancellationToken cancellationToken);

    Task<IReadOnlyList<Assignment>> LoadAssignmentsAsync(CancellationToken cancellationToken);
    Task SaveAssignmentsAsync(IReadOnlyList<Assignment> assignments, CancellationToken cancellationToken);

    Task<DateTimeOffset?> LoadLastSyncUtcAsync(CancellationToken cancellationToken);
    Task SaveLastSyncUtcAsync(DateTimeOffset syncedAtUtc, CancellationToken cancellationToken);

    /// <summary>Removes all cached user data (sign-out / local reset).</summary>
    Task ClearAsync(CancellationToken cancellationToken);
}
