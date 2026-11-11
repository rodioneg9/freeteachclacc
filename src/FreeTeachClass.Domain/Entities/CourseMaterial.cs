using FreeTeachClass.Domain.Enums;

namespace FreeTeachClass.Domain.Entities;

/// <summary>
/// A single material attached to course work (or, later, to a course).
/// Maps to the API <c>Material</c> union (driveFile / link / youtubeVideo / form).
/// The API gives no separate id for a material, so identity is derived.
/// </summary>
public sealed record CourseMaterial(
    string CourseId,
    string? AssignmentId,
    CourseMaterialKind Kind,
    string? Title,
    string? Url,
    string? DriveFileId,
    string? YouTubeVideoId);
