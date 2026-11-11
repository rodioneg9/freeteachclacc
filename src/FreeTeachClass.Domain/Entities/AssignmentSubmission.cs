using FreeTeachClass.Domain.Enums;

namespace FreeTeachClass.Domain.Entities;

/// <summary>
/// The signed-in student's submission for one course work item. Read-only
/// fields such as grades are filled in only when Google returns them.
/// </summary>
public sealed record AssignmentSubmission(
    string Id,
    string CourseId,
    string CourseWorkId,
    SubmissionState State,
    bool IsLate,
    double? AssignedGrade,
    double? DraftGrade,
    DateTimeOffset UpdateTime,
    IReadOnlyList<SubmissionAttachment> Attachments);
