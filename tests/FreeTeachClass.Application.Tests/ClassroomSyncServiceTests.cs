using FreeTeachClass.Application.Abstractions;
using FreeTeachClass.Application.Services;
using FreeTeachClass.Domain.Abstractions;
using FreeTeachClass.Domain.Entities;
using FreeTeachClass.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace FreeTeachClass.Application.Tests;

public class ClassroomSyncServiceTests
{
    [Fact]
    public async Task SyncWritesCoursesAssignmentsAndTimestampToStore()
    {
        var course = TestData.Course(id: "c1");
        var assignment = TestData.Assignment(id: "a1", courseId: "c1");
        var dataSource = new FakeDataSource(
            [course],
            new Dictionary<string, List<Assignment>> { ["c1"] = [assignment] });
        var store = new FakeStore();
        var service = new ClassroomSyncService(dataSource, store, NullLogger<ClassroomSyncService>.Instance);

        var result = await service.SyncAsync(CancellationToken.None);

        Assert.Equal(1, result.CoursesSynced);
        Assert.Equal(1, result.AssignmentsSynced);
        Assert.Equal(0, result.CoursesFailed);
        Assert.Single(store.Courses);
        Assert.Single(store.Assignments);
        Assert.NotNull(store.LastSyncUtc);
    }

    [Fact]
    public async Task FailingOneCourseDoesNotAbortTheWholeSync()
    {
        var good = TestData.Course(id: "good");
        var bad = TestData.Course(id: "bad");
        var dataSource = new FakeDataSource(
            [good, bad],
            new Dictionary<string, List<Assignment>>
            {
                ["good"] = [TestData.Assignment(courseId: "good")],
            });
        var store = new FakeStore();
        var service = new ClassroomSyncService(dataSource, store, NullLogger<ClassroomSyncService>.Instance);

        var result = await service.SyncAsync(CancellationToken.None);

        Assert.Equal(1, result.CoursesSynced);
        Assert.Equal(1, result.AssignmentsSynced);
        Assert.Equal(1, result.CoursesFailed);
        Assert.Single(store.Assignments);
        Assert.NotNull(store.LastSyncUtc);
    }

    [Fact]
    public async Task CancelledSyncThrowsAndKeepsStoreEmpty()
    {
        var dataSource = new FakeDataSource(
            [TestData.Course()],
            new Dictionary<string, List<Assignment>>());
        var store = new FakeStore();
        var service = new ClassroomSyncService(dataSource, store, NullLogger<ClassroomSyncService>.Instance);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SyncAsync(cts.Token));

        Assert.Empty(store.Courses);
        Assert.Empty(store.Assignments);
    }

    private sealed class FakeDataSource : IClassroomDataSource
    {
        private readonly IReadOnlyList<Course> _courses;
        private readonly IReadOnlyDictionary<string, List<Assignment>> _assignments;

        public FakeDataSource(
            IReadOnlyList<Course> courses,
            IReadOnlyDictionary<string, List<Assignment>> assignments)
        {
            _courses = courses;
            _assignments = assignments;
        }

        public Task<IReadOnlyList<Course>> GetCoursesAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_courses);
        }

        public Task<IReadOnlyList<Assignment>> GetAssignmentsAsync(
            string courseId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_assignments.TryGetValue(courseId, out var list))
            {
                throw new HttpRequestException($"course {courseId} unavailable");
            }

            return Task.FromResult<IReadOnlyList<Assignment>>(list);
        }
    }

    private sealed class FakeStore : ILocalDataStore
    {
        public List<Course> Courses { get; } = [];
        public List<Assignment> Assignments { get; } = [];
        public DateTimeOffset? LastSyncUtc { get; private set; }
        public UserProfile? User { get; private set; }

        public Task<UserProfile?> LoadUserAsync(CancellationToken cancellationToken) =>
            Task.FromResult(User);

        public Task SaveUserAsync(UserProfile user, CancellationToken cancellationToken)
        {
            User = user;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Course>> LoadCoursesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Course>>(Courses);

        public Task SaveCoursesAsync(IReadOnlyList<Course> courses, CancellationToken cancellationToken)
        {
            Courses.Clear();
            Courses.AddRange(courses);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Assignment>> LoadAssignmentsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Assignment>>(Assignments);

        public Task SaveAssignmentsAsync(
            IReadOnlyList<Assignment> assignments,
            CancellationToken cancellationToken)
        {
            Assignments.Clear();
            Assignments.AddRange(assignments);
            return Task.CompletedTask;
        }

        public Task<DateTimeOffset?> LoadLastSyncUtcAsync(CancellationToken cancellationToken) =>
            Task.FromResult(LastSyncUtc);

        public Task SaveLastSyncUtcAsync(DateTimeOffset syncedAtUtc, CancellationToken cancellationToken)
        {
            LastSyncUtc = syncedAtUtc;
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            Courses.Clear();
            Assignments.Clear();
            User = null;
            LastSyncUtc = null;
            return Task.CompletedTask;
        }
    }
}
