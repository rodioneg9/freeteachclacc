using System.Globalization;
using FreeTeachClass.Domain.Entities;
using FreeTeachClass.Domain.Enums;
using ApiAttachment = Google.Apis.Classroom.v1.Data.Attachment;
using ApiCourse = Google.Apis.Classroom.v1.Data.Course;
using ApiCourseWork = Google.Apis.Classroom.v1.Data.CourseWork;
using ApiDate = Google.Apis.Classroom.v1.Data.Date;
using ApiMaterial = Google.Apis.Classroom.v1.Data.Material;
using ApiStudentSubmission = Google.Apis.Classroom.v1.Data.StudentSubmission;
using ApiTimeOfDay = Google.Apis.Classroom.v1.Data.TimeOfDay;

namespace FreeTeachClass.Infrastructure.Google;

/// <summary>
/// Pure mapping from Google Classroom API DTOs to domain entities.
/// Only fields that exist in the official API are used; missing API values
/// become null / enum default instead of being invented.
/// </summary>
public static class GoogleClassroomMapper
{
    public static Course ToDomain(ApiCourse course) =>
        new(
            Id: course.Id ?? string.Empty,
            Name: string.IsNullOrWhiteSpace(course.Name) ? "Без названия" : course.Name,
            Section: NullIfWhiteSpace(course.Section),
            Room: NullIfWhiteSpace(course.Room),
            DescriptionHeading: NullIfWhiteSpace(course.DescriptionHeading),
            State: ParseCourseState(course.CourseState),
            CreationTime: ParseTimestamp(course.CreationTimeRaw) ?? DateTimeOffset.UnixEpoch,
            UpdateTime: ParseTimestamp(course.UpdateTimeRaw) ?? DateTimeOffset.UnixEpoch);

    public static Assignment ToDomain(ApiCourseWork work, string fallbackCourseId) =>
        new(
            Id: work.Id ?? string.Empty,
            CourseId: work.CourseId ?? fallbackCourseId,
            Title: string.IsNullOrWhiteSpace(work.Title) ? "Без названия" : work.Title,
            Description: NullIfWhiteSpace(work.Description),
            DueAt: ToLocalDueAt(work.DueDate, work.DueTime),
            State: ParseAssignmentState(work.State),
            Source: AssignmentSource.GoogleClassroom,
            WorkType: ParseWorkType(work.WorkType),
            CreationTime: ParseTimestamp(work.CreationTimeRaw) ?? DateTimeOffset.UnixEpoch,
            UpdateTime: ParseTimestamp(work.UpdateTimeRaw) ?? DateTimeOffset.UnixEpoch,
            MaxPoints: work.MaxPoints,
            TopicId: NullIfWhiteSpace(work.TopicId),
            Materials: MapMaterials(work, fallbackCourseId),
            AlternateLink: NullIfWhiteSpace(work.AlternateLink));

    public static AssignmentSubmission ToDomain(ApiStudentSubmission submission) =>
        new(
            Id: submission.Id ?? string.Empty,
            CourseId: submission.CourseId ?? string.Empty,
            CourseWorkId: submission.CourseWorkId ?? string.Empty,
            State: ParseSubmissionState(submission.State),
            IsLate: submission.Late ?? false,
            AssignedGrade: submission.AssignedGrade,
            DraftGrade: submission.DraftGrade,
            UpdateTime: ParseTimestamp(submission.UpdateTimeRaw) ?? DateTimeOffset.UnixEpoch,
            Attachments: submission.AssignmentSubmission?.Attachments is null
                ? Array.Empty<SubmissionAttachment>()
                : submission.AssignmentSubmission.Attachments.Select(ToDomain).ToArray());

    private static SubmissionAttachment ToDomain(ApiAttachment attachment)
    {
        if (attachment.DriveFile is not null)
        {
            return new SubmissionAttachment(
                CourseMaterialKind.DriveFile,
                attachment.DriveFile.Id,
                NullIfWhiteSpace(attachment.DriveFile.Title) ?? attachment.DriveFile.AlternateLink,
                attachment.DriveFile.AlternateLink);
        }

        if (attachment.Link is not null)
        {
            return new SubmissionAttachment(
                CourseMaterialKind.Link,
                DriveFileId: null,
                NullIfWhiteSpace(attachment.Link.Title) ?? attachment.Link.Url,
                attachment.Link.Url);
        }

        if (attachment.YouTubeVideo is not null)
        {
            return new SubmissionAttachment(
                CourseMaterialKind.YouTubeVideo,
                DriveFileId: null,
                NullIfWhiteSpace(attachment.YouTubeVideo.Title) ?? attachment.YouTubeVideo.AlternateLink,
                attachment.YouTubeVideo.AlternateLink);
        }

        if (attachment.Form is not null)
        {
            return new SubmissionAttachment(
                CourseMaterialKind.Form,
                DriveFileId: null,
                NullIfWhiteSpace(attachment.Form.Title) ?? attachment.Form.FormUrl,
                attachment.Form.FormUrl);
        }

        return new SubmissionAttachment(CourseMaterialKind.Unknown, null, null, null);
    }

    private static IReadOnlyList<CourseMaterial> MapMaterials(ApiCourseWork work, string fallbackCourseId)
    {
        if (work.Materials is null || work.Materials.Count == 0)
        {
            return Array.Empty<CourseMaterial>();
        }

        var courseId = work.CourseId ?? fallbackCourseId;
        return work.Materials
            .Select(material => ToDomain(material, courseId, work.Id))
            .ToArray();
    }

    private static CourseMaterial ToDomain(ApiMaterial material, string courseId, string? assignmentId)
    {
        // The API nests the file inside SharedDriveFile.
        var driveFile = material.DriveFile?.DriveFile;
        if (driveFile is not null)
        {
            return new CourseMaterial(
                courseId,
                assignmentId,
                CourseMaterialKind.DriveFile,
                driveFile.Title,
                driveFile.AlternateLink,
                driveFile.Id,
                YouTubeVideoId: null);
        }

        if (material.Link is not null)
        {
            return new CourseMaterial(
                courseId,
                assignmentId,
                CourseMaterialKind.Link,
                // A link may have no title; the URL is then the best name for it.
                NullIfWhiteSpace(material.Link.Title) ?? material.Link.Url,
                material.Link.Url,
                DriveFileId: null,
                YouTubeVideoId: null);
        }

        if (material.YoutubeVideo is not null)
        {
            return new CourseMaterial(
                courseId,
                assignmentId,
                CourseMaterialKind.YouTubeVideo,
                material.YoutubeVideo.Title,
                material.YoutubeVideo.AlternateLink,
                DriveFileId: null,
                material.YoutubeVideo.Id);
        }

        if (material.Form is not null)
        {
            return new CourseMaterial(
                courseId,
                assignmentId,
                CourseMaterialKind.Form,
                material.Form.Title,
                material.Form.FormUrl,
                DriveFileId: null,
                YouTubeVideoId: null);
        }

        return new CourseMaterial(
            courseId,
            assignmentId,
            CourseMaterialKind.Unknown,
            Title: null,
            Url: null,
            DriveFileId: null,
            YouTubeVideoId: null);
    }

    /// <summary>
    /// Google returns dueDate/dueTime without a time zone (the course time zone
    /// is implied). MVP decision: interpret them in the local machine zone, and
    /// when Google gives only a date use the end of that day — the same
    /// convention the Classroom UI shows ("due 11:59 PM").
    /// </summary>
    private static DateTimeOffset? ToLocalDueAt(ApiDate? dueDate, ApiTimeOfDay? dueTime)
    {
        if (dueDate?.Year is null or 0 || dueDate.Month is null or 0 || dueDate.Day is null or 0)
        {
            return null;
        }

        var date = new DateTime(dueDate.Year.Value, dueDate.Month.Value, dueDate.Day.Value);

        var time = dueTime is null
            ? new TimeSpan(23, 59, 59)
            : new TimeSpan(dueTime.Hours ?? 0, dueTime.Minutes ?? 0, dueTime.Seconds ?? 0);

        // DateTime has Kind=Unspecified, so DateTimeOffset treats it as local time.
        return new DateTimeOffset(date + time);
    }

    /// <summary>
    /// The API declares timestamps as <c>google-datetime</c>; in practice they
    /// arrive either as an RFC 3339 string or as epoch milliseconds. Parsing the
    /// raw value ourselves keeps both shapes working and never throws on an
    /// unexpected format — a bad timestamp degrades to the epoch.
    /// </summary>
    private static DateTimeOffset? ParseTimestamp(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var milliseconds))
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
        }

        return DateTimeOffset.TryParse(
            raw,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed
            : null;
    }

    private static CourseState ParseCourseState(string? state) => state?.ToUpperInvariant() switch
    {
        "ACTIVE" => CourseState.Active,
        "ARCHIVED" => CourseState.Archived,
        "PROVISIONED" => CourseState.Provisioned,
        "DECLINED" => CourseState.Declined,
        "SUSPENDED" => CourseState.Suspended,
        _ => CourseState.Unspecified,
    };

    private static AssignmentState ParseAssignmentState(string? state) => state?.ToUpperInvariant() switch
    {
        "PUBLISHED" => AssignmentState.Published,
        "DRAFT" => AssignmentState.Draft,
        _ => AssignmentState.Unspecified,
    };

    private static SubmissionState ParseSubmissionState(string? state) => state?.ToUpperInvariant() switch
    {
        "NEW" => SubmissionState.New,
        "CREATED" => SubmissionState.Created,
        "TURNED_IN" => SubmissionState.TurnedIn,
        "RETURNED" => SubmissionState.Returned,
        "RECLAIMED" => SubmissionState.Reclaimed,
        _ => SubmissionState.Unspecified,
    };

    private static AssignmentWorkType ParseWorkType(string? workType) => workType?.ToUpperInvariant() switch
    {
        "ASSIGNMENT" => AssignmentWorkType.Assignment,
        "SHORT_ANSWER_QUESTION" => AssignmentWorkType.ShortAnswerQuestion,
        "MULTIPLE_CHOICE_QUESTION" => AssignmentWorkType.MultipleChoiceQuestion,
        _ => AssignmentWorkType.Unspecified,
    };

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
