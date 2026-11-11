using FreeTeachClass.Domain.Entities;

namespace FreeTeachClass.Application.Services;

/// <summary>A line in the "Сегодня" section of the dashboard.</summary>
public sealed record TodayEntry(
    string AssignmentId,
    string CourseId,
    string Title,
    string CourseName,
    DateTimeOffset DueAt,
    bool IsOverdue);

/// <summary>Everything the dashboard needs, computed from the local cache.</summary>
public sealed record DashboardSnapshot(
    int CourseCount,
    int ActiveAssignmentCount,
    Assignment? NearestDeadline,
    string? NearestDeadlineCourseName,
    IReadOnlyList<TodayEntry> TodayEntries);
