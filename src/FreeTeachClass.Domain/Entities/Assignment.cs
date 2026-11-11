using FreeTeachClass.Domain.Enums;

namespace FreeTeachClass.Domain.Entities;

/// <summary>
/// A piece of course work (assignment/question) from Google Classroom.
/// Maps to the <c>CourseWork</c> resource; fields absent in the API stay null.
/// <para>
/// Google returns due date/time without a time zone (it is expressed in the
/// course time zone). <see cref="DueAt"/> is normalised to the local machine
/// zone by the infrastructure mapper — a documented MVP limitation.
/// </para>
/// </summary>
public sealed record Assignment(
    string Id,
    string CourseId,
    string Title,
    string? Description,
    DateTimeOffset? DueAt,
    AssignmentState State,
    AssignmentSource Source,
    AssignmentWorkType WorkType,
    DateTimeOffset CreationTime,
    DateTimeOffset UpdateTime,
    double? MaxPoints,
    string? TopicId,
    IReadOnlyList<CourseMaterial> Materials,
    string? AlternateLink = null)
{
    /// <summary>True when the assignment is published and its deadline is not in the past.</summary>
    public bool IsActiveAt(DateTimeOffset moment) =>
        State == AssignmentState.Published
        && (DueAt is null || DueAt.Value >= moment);
}
