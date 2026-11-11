namespace FreeTeachClass.Domain.Enums;

/// <summary>Mirrors the Google Classroom API <c>CourseState</c> enum.</summary>
public enum CourseState
{
    Unspecified = 0,
    Active = 1,
    Archived = 2,
    Provisioned = 3,
    Declined = 4,
    Suspended = 5,
}
