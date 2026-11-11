using FreeTeachClass.Domain.Enums;

namespace FreeTeachClass.Domain.Entities;

/// <summary>
/// One attachment of a student submission. Mirrors the API <c>Attachment</c>
/// union (Drive file / link / YouTube video / form).
/// </summary>
public sealed record SubmissionAttachment(
    CourseMaterialKind Kind,
    string? DriveFileId,
    string? Title,
    string? Url);
