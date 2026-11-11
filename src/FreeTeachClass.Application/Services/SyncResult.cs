namespace FreeTeachClass.Application.Services;

/// <summary>
/// Outcome of one sync run. Contains counts only — no personal data.
/// <c>CoursesSynced</c> counts courses fetched successfully; failures land in
/// <c>CoursesFailed</c>.
/// </summary>
public sealed record SyncResult(
    int CoursesSynced,
    int AssignmentsSynced,
    int CoursesFailed,
    DateTimeOffset SyncedAtUtc);
