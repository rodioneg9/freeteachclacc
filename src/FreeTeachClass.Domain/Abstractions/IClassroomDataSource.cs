using FreeTeachClass.Domain.Entities;

namespace FreeTeachClass.Domain.Abstractions;

/// <summary>
/// Remote source of Classroom data. The UI layer must never call this directly;
/// the Application layer owns the sync flow.
/// </summary>
public interface IClassroomDataSource
{
    /// <summary>Fetches every course the user can see (active by default), following pagination.</summary>
    Task<IReadOnlyList<Course>> GetCoursesAsync(CancellationToken cancellationToken);

    /// <summary>Fetches the coursework assigned to the user in a single course.</summary>
    Task<IReadOnlyList<Assignment>> GetAssignmentsAsync(string courseId, CancellationToken cancellationToken);
}
