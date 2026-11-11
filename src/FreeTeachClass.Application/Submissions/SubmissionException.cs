namespace FreeTeachClass.Application.Submissions;

/// <summary>Why a submission attempt could not be completed.</summary>
public enum SubmissionFailureKind
{
    UnsupportedWorkType,
    NoSubmission,
    TooManyAttachments,
    InvalidLink,
    PermissionDenied,
    DriveUploadFailed,
    Network,
    Unknown,
}

/// <summary>
/// A failure that is safe to show to the user. Infrastructure maps Google
/// errors to one of <see cref="SubmissionFailureKind"/> without exposing HTTP
/// bodies or tokens.
/// </summary>
public sealed class SubmissionException : Exception
{
    public SubmissionException(
        SubmissionFailureKind kind,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }

    public SubmissionFailureKind Kind { get; }
}
