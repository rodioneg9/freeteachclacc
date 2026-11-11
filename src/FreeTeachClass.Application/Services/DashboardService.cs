using FreeTeachClass.Domain.Entities;

namespace FreeTeachClass.Application.Services;

public interface IDashboardService
{
    DashboardSnapshot BuildSnapshot(
        IReadOnlyList<Course> courses,
        IReadOnlyList<Assignment> assignments,
        DateTimeOffset now);
}

/// <summary>
/// Pure dashboard computations over cached data. No I/O, no Google types —
/// fully unit-testable.
/// <para>Business rules chosen for the MVP (documented in README):</para>
/// <list type="bullet">
/// <item>An assignment is "active" when it is published and not overdue (assignments without a due date count as active).</item>
/// <item>"Сегодня" lists published assignments due today plus everything overdue.</item>
/// <item>Draft coursework (only visible to teachers) is ignored.</item>
/// </list>
/// </summary>
public sealed class DashboardService : IDashboardService
{
    public DashboardSnapshot BuildSnapshot(
        IReadOnlyList<Course> courses,
        IReadOnlyList<Assignment> assignments,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(courses);
        ArgumentNullException.ThrowIfNull(assignments);

        var courseNames = courses.ToDictionary(c => c.Id, c => c.Name, StringComparer.Ordinal);

        var published = assignments
            .Where(a => a.State == Domain.Enums.AssignmentState.Published)
            .ToList();

        var active = published.Where(a => a.IsActiveAt(now)).ToList();

        var nearest = active
            .Where(a => a.DueAt is not null)
            .OrderBy(a => a.DueAt)
            .FirstOrDefault();

        var todayEntries = published
            .Where(a => a.DueAt is not null && a.DueAt.Value < now)
            .Select(a => ToEntry(a, courseNames, isOverdue: true))
            .Concat(published
                .Where(a => a.DueAt is not null
                            && a.DueAt.Value >= now
                            && a.DueAt.Value.Date == now.Date)
                .Select(a => ToEntry(a, courseNames, isOverdue: false)))
            .OrderBy(e => e.IsOverdue ? 0 : 1)
            .ThenBy(e => e.DueAt)
            .ToList();

        return new DashboardSnapshot(
            CourseCount: courses.Count,
            ActiveAssignmentCount: active.Count,
            NearestDeadline: nearest,
            NearestDeadlineCourseName: nearest is null ? null : courseNames.GetValueOrDefault(nearest.CourseId),
            TodayEntries: todayEntries);
    }

    private static TodayEntry ToEntry(
        Assignment assignment,
        IReadOnlyDictionary<string, string> courseNames,
        bool isOverdue) =>
        new(
            AssignmentId: assignment.Id,
            CourseId: assignment.CourseId,
            Title: assignment.Title,
            CourseName: courseNames.GetValueOrDefault(assignment.CourseId, "—"),
            DueAt: assignment.DueAt!.Value,
            IsOverdue: isOverdue);
}
