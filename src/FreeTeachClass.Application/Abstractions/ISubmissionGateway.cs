using FreeTeachClass.Application.Submissions;
using FreeTeachClass.Domain.Entities;

namespace FreeTeachClass.Application.Abstractions;

/// <summary>
/// Write side of the Classroom submission API plus the Drive upload it needs.
/// Implemented in Infrastructure; the Application layer owns the order of
/// operations and the duplicate checks.
/// </summary>
public interface ISubmissionGateway
{
    /// <summary>Returns the signed-in student's submission, or null when none exists.</summary>
    Task<AssignmentSubmission?> GetStudentSubmissionAsync(
        string courseId,
        string courseWorkId,
        CancellationToken cancellationToken);

    /// <summary>Adds a link attachment. Only assignments support attachments.</summary>
    Task<AssignmentSubmission> AddLinkAsync(
        string courseId,
        string courseWorkId,
        string submissionId,
        string url,
        string title,
        CancellationToken cancellationToken);

    /// <summary>Uploads a local file to Drive and adds it as an attachment.</summary>
    Task<AssignmentSubmission> AddFileAsync(
        string courseId,
        string courseWorkId,
        string submissionId,
        SubmissionFile file,
        CancellationToken cancellationToken);

    /// <summary>Turns in the submission and transfers attached Drive files to the teacher.</summary>
    Task<AssignmentSubmission> TurnInAsync(
        string courseId,
        string courseWorkId,
        string submissionId,
        CancellationToken cancellationToken);
}
