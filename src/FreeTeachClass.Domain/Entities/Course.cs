using FreeTeachClass.Domain.Enums;

namespace FreeTeachClass.Domain.Entities;

/// <summary>
/// A Google Classroom course the user is enrolled in as a student.
/// Only fields that really exist in the Google Classroom API are modelled;
/// API-optional fields are nullable.
/// </summary>
public sealed record Course(
    string Id,
    string Name,
    string? Section,
    string? Room,
    string? DescriptionHeading,
    CourseState State,
    DateTimeOffset CreationTime,
    DateTimeOffset UpdateTime);
