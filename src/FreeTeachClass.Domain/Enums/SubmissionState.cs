namespace FreeTeachClass.Domain.Enums;

/// <summary>State of the signed-in student's submission for one course work item.</summary>
public enum SubmissionState
{
    Unspecified,
    New,
    Created,
    TurnedIn,
    Returned,
    Reclaimed,
}
