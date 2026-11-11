using FreeTeachClass.Application.Abstractions;
using FreeTeachClass.Application.Submissions;
using FreeTeachClass.Domain.Entities;
using FreeTeachClass.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace FreeTeachClass.Application.Submissions;

public interface IAssignmentSubmissionService
{
    /// <summary>Reads the current student submission; null when Google has none.</summary>
    Task<AssignmentSubmission?> GetAsync(
        string courseId,
        string courseWorkId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Adds the chosen files and/or link, then turns the work in. Uses the
    /// Classroom API only; failures surface as <see cref="SubmissionException"/>
    /// so the UI can offer the web fallback.
    /// </summary>
    Task<SubmissionOutcome> SubmitAsync(
        Assignment assignment,
        IReadOnlyList<SubmissionFile> files,
        string? linkUrl,
        CancellationToken cancellationToken);
}

public sealed record SubmissionOutcome(
    AssignmentSubmission Submission,
    int AddedAttachments,
    bool TurnedIn);

/// <summary>
/// Orchestrates the submission API. Attachments are added one at a time so a
/// file upload failure does not turn in a half-attached submission; the final
/// turn-in is a separate step.
/// </summary>
public sealed class AssignmentSubmissionService : IAssignmentSubmissionService
{
    private const int MaxAttachments = 20;

    private readonly ISubmissionGateway _gateway;
    private readonly ILogger<AssignmentSubmissionService> _logger;

    public AssignmentSubmissionService(
        ISubmissionGateway gateway,
        ILogger<AssignmentSubmissionService> logger)
    {
        _gateway = gateway;
        _logger = logger;
    }

    public Task<AssignmentSubmission?> GetAsync(
        string courseId,
        string courseWorkId,
        CancellationToken cancellationToken) =>
        _gateway.GetStudentSubmissionAsync(courseId, courseWorkId, cancellationToken);

    public async Task<SubmissionOutcome> SubmitAsync(
        Assignment assignment,
        IReadOnlyList<SubmissionFile> files,
        string? linkUrl,
        CancellationToken cancellationToken)
    {
        if (assignment.WorkType != AssignmentWorkType.Assignment)
        {
            throw new SubmissionException(
                SubmissionFailureKind.UnsupportedWorkType,
                "Через API можно сдавать только задания типа «Задание». " +
                "Вопросы с кратким ответом и тесты нужно сдавать на сайте Classroom.");
        }

        var link = string.IsNullOrWhiteSpace(linkUrl) ? null : linkUrl.Trim();
        if (link is not null
            && (!Uri.TryCreate(link, UriKind.Absolute, out var linkUri)
                || linkUri.Scheme is not ("http" or "https")))
        {
            throw new SubmissionException(
                SubmissionFailureKind.InvalidLink,
                "Ссылка должна начинаться с http:// или https://.");
        }

        var submission = await _gateway
            .GetStudentSubmissionAsync(assignment.CourseId, assignment.Id, cancellationToken)
            .ConfigureAwait(false);

        if (submission is null)
        {
            throw new SubmissionException(
                SubmissionFailureKind.NoSubmission,
                "Google не вернул вашу работу для этого задания. " +
                "Откройте задание в Classroom и попробуйте ещё раз.");
        }

        if (submission.State == SubmissionState.TurnedIn)
        {
            return new SubmissionOutcome(submission, 0, TurnedIn: true);
        }

        var added = 0;
        var knownLinkUrls = submission.Attachments
            .Where(attachment => attachment.Kind == CourseMaterialKind.Link)
            .Select(attachment => attachment.Url)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (link is not null && !knownLinkUrls.Contains(link))
        {
            if (submission.Attachments.Count >= MaxAttachments)
            {
                throw TooManyAttachments();
            }

            submission = await _gateway
                .AddLinkAsync(
                    assignment.CourseId,
                    assignment.Id,
                    submission.Id,
                    link,
                    LinkTitle(link),
                    cancellationToken)
                .ConfigureAwait(false);
            added++;
        }

        var knownFileTitles = submission.Attachments
            .Where(attachment => attachment.Kind == CourseMaterialKind.DriveFile)
            .Select(attachment => attachment.Title)
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            if (!knownFileTitles.Add(file.Title))
            {
                continue;
            }

            if (submission.Attachments.Count >= MaxAttachments)
            {
                throw TooManyAttachments();
            }

            submission = await _gateway
                .AddFileAsync(
                    assignment.CourseId,
                    assignment.Id,
                    submission.Id,
                    file,
                    cancellationToken)
                .ConfigureAwait(false);
            added++;
        }

        // The turn-in request is what transfers ownership of attached Drive
        // files to the teacher. Keep it as the last step.
        submission = await _gateway
            .TurnInAsync(assignment.CourseId, assignment.Id, submission.Id, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Submission turned in for course {CourseId}, coursework {CourseWorkId}, attachments added: {Count}",
            assignment.CourseId,
            assignment.Id,
            added);

        return new SubmissionOutcome(submission, added, TurnedIn: true);
    }

    private static SubmissionException TooManyAttachments() =>
        new(
            SubmissionFailureKind.TooManyAttachments,
            $"К работе можно прикрепить не больше {MaxAttachments} вложений.");

    private static string LinkTitle(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host)
            ? uri.Host
            : "Ссылка";
}
