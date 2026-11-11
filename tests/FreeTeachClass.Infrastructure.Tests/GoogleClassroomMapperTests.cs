using FreeTeachClass.Domain.Enums;
using FreeTeachClass.Infrastructure.Google;
using Google.Apis.Classroom.v1.Data;
using ApiCourse = Google.Apis.Classroom.v1.Data.Course;
using ApiCourseWork = Google.Apis.Classroom.v1.Data.CourseWork;

namespace FreeTeachClass.Infrastructure.Tests;

/// <summary>
/// Mapping from Google Classroom API DTOs to domain entities. The due-date
/// cases lock in the documented MVP convention: date/time without a zone is
/// interpreted in the local machine zone, a date-only due means the end of
/// that day.
/// </summary>
public class GoogleClassroomMapperTests
{
    [Fact]
    public void Course_MapsFieldsThatExistInTheApi()
    {
        var api = new ApiCourse
        {
            Id = "c1",
            Name = "Математика",
            Section = "10А",
            Room = "214",
            DescriptionHeading = "Математика — 10 класс",
            CourseState = "ACTIVE",
            CreationTimeRaw = "1760000000000",
            UpdateTimeRaw = "1760064000000",
        };

        var course = GoogleClassroomMapper.ToDomain(api);

        Assert.Equal("c1", course.Id);
        Assert.Equal("Математика", course.Name);
        Assert.Equal("10А", course.Section);
        Assert.Equal("214", course.Room);
        Assert.Equal("Математика — 10 класс", course.DescriptionHeading);
        Assert.Equal(CourseState.Active, course.State);
        Assert.Equal(1760000000000, course.CreationTime.ToUnixTimeMilliseconds());
        Assert.Equal(1760064000000, course.UpdateTime.ToUnixTimeMilliseconds());
    }

    [Fact]
    public void Course_Rfc3339Timestamp_IsReadAsUtcInstant()
    {
        var course = GoogleClassroomMapper.ToDomain(new ApiCourse
        {
            Id = "c3",
            Name = "История",
            CreationTimeRaw = "2026-10-09T12:34:56.789Z",
            UpdateTimeRaw = "not a timestamp at all",
        });

        var expected = new DateTimeOffset(2026, 10, 9, 12, 34, 56, TimeSpan.Zero)
            .AddMilliseconds(789);
        Assert.Equal(expected, course.CreationTime);

        // A malformed value degrades to the epoch instead of throwing.
        Assert.Equal(DateTimeOffset.UnixEpoch, course.UpdateTime);
    }

    [Fact]
    public void Course_WithoutNameOrOptionalFields_BecomesPlaceholderAndNulls()
    {
        var course = GoogleClassroomMapper.ToDomain(new ApiCourse { Id = "c2" });

        Assert.Equal("Без названия", course.Name);
        Assert.Null(course.Section);
        Assert.Null(course.Room);
        Assert.Null(course.DescriptionHeading);
        Assert.Equal(CourseState.Unspecified, course.State);
        Assert.Equal(DateTimeOffset.UnixEpoch, course.CreationTime);
    }

    [Fact]
    public void Assignment_DueDateOnly_UsesEndOfLocalDay()
    {
        var api = new ApiCourseWork
        {
            Id = "w1",
            Title = "Домашняя работа",
            State = "PUBLISHED",
            WorkType = "ASSIGNMENT",
            DueDate = new Date { Year = 2026, Month = 10, Day = 20 },
        };

        var work = GoogleClassroomMapper.ToDomain(api, "course-1");

        // 23:59:59 local time, exactly as Classroom's UI shows "due 11:59 PM".
        var expected = new DateTimeOffset(new DateTime(2026, 10, 20, 23, 59, 59));
        Assert.Equal(expected, work.DueAt);
    }

    [Fact]
    public void Assignment_DueDateAndDueTime_UsesTheGivenLocalTime()
    {
        var api = new ApiCourseWork
        {
            Id = "w2",
            Title = "Тест",
            State = "PUBLISHED",
            DueDate = new Date { Year = 2026, Month = 11, Day = 1 },
            DueTime = new TimeOfDay { Hours = 12, Minutes = 30, Seconds = 0 },
        };

        var work = GoogleClassroomMapper.ToDomain(api, "course-1");

        var expected = new DateTimeOffset(new DateTime(2026, 11, 1, 12, 30, 0));
        Assert.Equal(expected, work.DueAt);
    }

    [Fact]
    public void Assignment_WithoutDueDate_HasNoDeadline()
    {
        var work = GoogleClassroomMapper.ToDomain(
            new ApiCourseWork { Id = "w3", Title = "Без срока", State = "PUBLISHED" },
            "course-1");

        Assert.Null(work.DueAt);
    }

    [Fact]
    public void Assignment_MapsStateWorkTypeSourceAndCourseIdFallback()
    {
        var api = new ApiCourseWork
        {
            Id = "w4",
            Title = "Черновик",
            State = "DRAFT",
            WorkType = "SHORT_ANSWER_QUESTION",
            MaxPoints = 25,
            CreationTimeRaw = "1760000000000",
            UpdateTimeRaw = "1760003600000",
        };

        var work = GoogleClassroomMapper.ToDomain(api, fallbackCourseId: "course-42");

        Assert.Equal("course-42", work.CourseId);
        Assert.Equal(AssignmentState.Draft, work.State);
        Assert.Equal(AssignmentWorkType.ShortAnswerQuestion, work.WorkType);
        Assert.Equal(AssignmentSource.GoogleClassroom, work.Source);
        Assert.Equal(25d, work.MaxPoints);
        Assert.Equal(1760000000000, work.CreationTime.ToUnixTimeMilliseconds());
        Assert.Equal(1760003600000, work.UpdateTime.ToUnixTimeMilliseconds());
    }

    [Fact]
    public void Assignment_PrefersCourseIdFromTheApiOverTheFallback()
    {
        var work = GoogleClassroomMapper.ToDomain(
            new ApiCourseWork { Id = "w5", Title = "X", CourseId = "from-api" },
            "fallback");

        Assert.Equal("from-api", work.CourseId);
    }

    [Fact]
    public void Materials_MapEverySupportedKindOfTheApiUnion()
    {
        var api = new ApiCourseWork
        {
            Id = "w6",
            Title = "С материалами",
            CourseId = "c9",
            Materials =
            [
                new Material
                {
                    DriveFile = new SharedDriveFile
                    {
                        DriveFile = new DriveFile
                        {
                            Id = "file-1",
                            Title = "Отчёт.docx",
                            AlternateLink = "https://drive.google.com/file/d/file-1",
                        },
                    },
                },
                new Material { Link = new Link { Url = "https://example.com/article", Title = "Статья" } },
                new Material
                {
                    YoutubeVideo = new YouTubeVideo
                    {
                        Id = "vid-1",
                        Title = "Лекция 1",
                        AlternateLink = "https://youtu.be/vid-1",
                    },
                },
                new Material
                {
                    Form = new Form
                    {
                        Title = "Опрос",
                        FormUrl = "https://docs.google.com/forms/d/form-1",
                    },
                },
            ],
        };

        var work = GoogleClassroomMapper.ToDomain(api, "ignored");

        Assert.Equal(4, work.Materials.Count);

        var drive = work.Materials[0];
        Assert.Equal(CourseMaterialKind.DriveFile, drive.Kind);
        Assert.Equal("Отчёт.docx", drive.Title);
        Assert.Equal("file-1", drive.DriveFileId);
        Assert.Equal("https://drive.google.com/file/d/file-1", drive.Url);
        Assert.Equal("c9", drive.CourseId);
        Assert.Equal("w6", drive.AssignmentId);

        var link = work.Materials[1];
        Assert.Equal(CourseMaterialKind.Link, link.Kind);
        Assert.Equal("https://example.com/article", link.Url);
        Assert.Equal("Статья", link.Title);

        var video = work.Materials[2];
        Assert.Equal(CourseMaterialKind.YouTubeVideo, video.Kind);
        Assert.Equal("vid-1", video.YouTubeVideoId);
        Assert.Equal("Лекция 1", video.Title);

        var form = work.Materials[3];
        Assert.Equal(CourseMaterialKind.Form, form.Kind);
        Assert.Equal("Опрос", form.Title);
        Assert.Equal("https://docs.google.com/forms/d/form-1", form.Url);
    }

    [Fact]
    public void Material_OfUnknownShape_MapsToKindUnknownInsteadOfBeingDropped()
    {
        var work = GoogleClassroomMapper.ToDomain(
            new ApiCourseWork { Id = "w7", Title = "X", Materials = [new Material()] },
            "c1");

        var material = Assert.Single(work.Materials);
        Assert.Equal(CourseMaterialKind.Unknown, material.Kind);
    }

    [Fact]
    public void Assignment_WithoutMaterials_HasEmptyMaterialList()
    {
        var work = GoogleClassroomMapper.ToDomain(new ApiCourseWork { Id = "w8" }, "c1");

        Assert.Empty(work.Materials);
    }
}
