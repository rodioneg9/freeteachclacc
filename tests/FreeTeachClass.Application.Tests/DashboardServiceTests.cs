using FreeTeachClass.Application.Services;
using FreeTeachClass.Domain.Enums;

namespace FreeTeachClass.Application.Tests;

public class DashboardServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 10, 0, 0, TimeSpan.Zero);

    private readonly DashboardService _service = new();

    [Fact]
    public void EmptyInputs_ProduceEmptySnapshot()
    {
        var snapshot = _service.BuildSnapshot([], [], Now);

        Assert.Equal(0, snapshot.CourseCount);
        Assert.Equal(0, snapshot.ActiveAssignmentCount);
        Assert.Null(snapshot.NearestDeadline);
        Assert.Empty(snapshot.TodayEntries);
    }

    [Fact]
    public void DraftsAreNeverActive()
    {
        var draft = TestData.Assignment(state: AssignmentState.Draft, dueAt: Now.AddDays(1));

        var snapshot = _service.BuildSnapshot([TestData.Course()], [draft], Now);

        Assert.Equal(0, snapshot.ActiveAssignmentCount);
        Assert.Null(snapshot.NearestDeadline);
        Assert.Empty(snapshot.TodayEntries);
    }

    [Fact]
    public void OverdueAssignmentIsNotActiveButAppearsInTodaySection()
    {
        var overdue = TestData.Assignment(id: "late", dueAt: Now.AddHours(-2));

        var snapshot = _service.BuildSnapshot([TestData.Course()], [overdue], Now);

        Assert.Equal(0, snapshot.ActiveAssignmentCount);
        var entry = Assert.Single(snapshot.TodayEntries);
        Assert.True(entry.IsOverdue);
    }

    [Fact]
    public void AssignmentWithoutDueDateCountsAsActive()
    {
        var noDeadline = TestData.Assignment(dueAt: null);

        var snapshot = _service.BuildSnapshot([TestData.Course()], [noDeadline], Now);

        Assert.Equal(1, snapshot.ActiveAssignmentCount);
        Assert.Null(snapshot.NearestDeadline);
        Assert.Empty(snapshot.TodayEntries);
    }

    [Fact]
    public void AssignmentDueTodayIsBothActiveAndInTodaySection()
    {
        var dueTonight = TestData.Assignment(dueAt: Now.ToLocalTime().Date.AddDays(1).AddHours(-1));

        var snapshot = _service.BuildSnapshot([TestData.Course()], [dueTonight], Now);

        Assert.Equal(1, snapshot.ActiveAssignmentCount);
        var entry = Assert.Single(snapshot.TodayEntries);
        Assert.False(entry.IsOverdue);
    }

    [Fact]
    public void NearestDeadlineIsSoonestFutureDueDate()
    {
        var soon = TestData.Assignment(id: "soon", dueAt: Now.AddHours(3));
        var later = TestData.Assignment(id: "later", dueAt: Now.AddDays(5));
        var wayLater = TestData.Assignment(id: "way", dueAt: null);

        var snapshot = _service.BuildSnapshot(
            [TestData.Course()],
            [later, wayLater, soon],
            Now);

        Assert.Equal("soon", snapshot.NearestDeadline?.Id);
    }

    [Fact]
    public void CourseNamesAreResolvedForDeadlinesAndTodayEntries()
    {
        var course = TestData.Course(id: "c-42", name: "Физика");
        var dueToday = TestData.Assignment(
            id: "work",
            courseId: "c-42",
            dueAt: Now.ToLocalTime().Date.AddDays(1).AddHours(-1));

        var snapshot = _service.BuildSnapshot([course], [dueToday], Now);

        Assert.Equal("Физика", snapshot.NearestDeadlineCourseName);
        Assert.Equal("Физика", Assert.Single(snapshot.TodayEntries).CourseName);
    }

    [Fact]
    public void TodayEntriesPutOverdueFirstAndSortByDeadline()
    {
        var overdueOld = TestData.Assignment(id: "old", dueAt: Now.AddDays(-2));
        var overdueNew = TestData.Assignment(id: "newer", dueAt: Now.AddHours(-1));
        var dueLaterToday = TestData.Assignment(id: "today", dueAt: Now.ToLocalTime().Date.AddDays(1).AddHours(-1));

        var snapshot = _service.BuildSnapshot(
            [TestData.Course()],
            [dueLaterToday, overdueOld, overdueNew],
            Now);

        Assert.Equal(["old", "newer", "today"], snapshot.TodayEntries.Select(e => e.AssignmentId));
    }

    [Fact]
    public void CourseCountCountsEveryCourseIncludingArchived()
    {
        var courses = new[]
        {
            TestData.Course(id: "1"),
            TestData.Course(id: "2"),
            TestData.Course(id: "3", state: CourseState.Archived),
        };

        var snapshot = _service.BuildSnapshot(courses, [], Now);

        Assert.Equal(3, snapshot.CourseCount);
    }
}
