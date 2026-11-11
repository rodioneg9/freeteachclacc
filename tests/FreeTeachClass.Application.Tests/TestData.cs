using FreeTeachClass.Application.Services;
using FreeTeachClass.Domain.Entities;
using FreeTeachClass.Domain.Enums;

namespace FreeTeachClass.Application.Tests;

internal static class TestData
{
    public static Course Course(
        string id = "course-1",
        string name = "Математика",
        CourseState state = CourseState.Active) =>
        new(id, name, Section: null, Room: null, DescriptionHeading: null, state,
            CreationTime: DateTimeOffset.UnixEpoch, UpdateTime: DateTimeOffset.UnixEpoch);

    public static Assignment Assignment(
        string id = "work-1",
        string courseId = "course-1",
        string title = "Домашка",
        DateTimeOffset? dueAt = null,
        AssignmentState state = AssignmentState.Published) =>
        new(
            Id: id,
            CourseId: courseId,
            Title: title,
            Description: null,
            DueAt: dueAt,
            State: state,
            Source: AssignmentSource.GoogleClassroom,
            WorkType: AssignmentWorkType.Assignment,
            CreationTime: DateTimeOffset.UnixEpoch,
            UpdateTime: DateTimeOffset.UnixEpoch,
            MaxPoints: null,
            TopicId: null,
            Materials: Array.Empty<CourseMaterial>());
}
