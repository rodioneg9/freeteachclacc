namespace FreeTeachClass.Domain.Enums;

/// <summary>
/// Internal marker of where an assignment came from. The Google Classroom API
/// has no "source" field on CourseWork, so this is a domain-level concept that
/// will allow merging data from other sources in the future.
/// </summary>
public enum AssignmentSource
{
    Unspecified = 0,
    GoogleClassroom = 1,
}
