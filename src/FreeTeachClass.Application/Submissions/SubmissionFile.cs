namespace FreeTeachClass.Application.Submissions;

/// <summary>
/// One local file chosen by the user for submission. The application layer
/// passes it to the gateway, which uploads the file to Google Drive and attaches
/// it to the student submission.
/// </summary>
public sealed record SubmissionFile(string Path, string Title);
