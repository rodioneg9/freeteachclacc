using FreeTeachClass.Application.Abstractions;
using FreeTeachClass.Application.Submissions;
using FreeTeachClass.Domain.Entities;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Classroom.v1;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Microsoft.Extensions.Logging;
using ApiAttachment = Google.Apis.Classroom.v1.Data.Attachment;
using ApiDriveFile = Google.Apis.Classroom.v1.Data.DriveFile;
using ApiLink = Google.Apis.Classroom.v1.Data.Link;
using ApiModifyAttachmentsRequest = Google.Apis.Classroom.v1.Data.ModifyAttachmentsRequest;
using ApiTurnInRequest = Google.Apis.Classroom.v1.Data.TurnInStudentSubmissionRequest;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace FreeTeachClass.Infrastructure.Google;

/// <summary>
/// Calls the Classroom student-submission endpoints and uploads files to Drive
/// with the signed-in user's credential. Google restricts
/// <c>modifyAttachments</c> and <c>turnIn</c> to the Cloud project that created
/// the coursework, so these calls legitimately fail for work created in the
/// Classroom UI; the UI turns that failure into a warning and a web link.
/// </summary>
public sealed class GoogleSubmissionGateway : ISubmissionGateway
{
    private const string ApplicationName = "FreeTeachClass";

    private readonly IGoogleCredentialProvider _credentialProvider;
    private readonly ILogger<GoogleSubmissionGateway> _logger;

    public GoogleSubmissionGateway(
        IGoogleCredentialProvider credentialProvider,
        ILogger<GoogleSubmissionGateway> logger)
    {
        _credentialProvider = credentialProvider;
        _logger = logger;
    }

    public async Task<AssignmentSubmission?> GetStudentSubmissionAsync(
        string courseId,
        string courseWorkId,
        CancellationToken cancellationToken)
    {
        try
        {
            var credential = await _credentialProvider
                .GetCredentialAsync(cancellationToken)
                .ConfigureAwait(false);
            using var service = CreateClassroomService(credential);

            var request = service.Courses.CourseWork.StudentSubmissions.List(courseId, courseWorkId);
            request.UserId = "me";
            request.PageSize = 10;

            var page = await request.ExecuteAsync(cancellationToken).ConfigureAwait(false);
            var submission = page.StudentSubmissions?.FirstOrDefault();
            return submission is null ? null : GoogleClassroomMapper.ToDomain(submission);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && ex is not SubmissionException)
        {
            throw MapClassroomError(ex);
        }
    }

    public async Task<AssignmentSubmission> AddLinkAsync(
        string courseId,
        string courseWorkId,
        string submissionId,
        string url,
        string title,
        CancellationToken cancellationToken)
    {
        try
        {
            var credential = await _credentialProvider
                .GetCredentialAsync(cancellationToken)
                .ConfigureAwait(false);

            var attachment = new ApiAttachment
            {
                Link = new ApiLink
                {
                    Url = url,
                    Title = title,
                },
            };

            return await ModifyAttachmentsAsync(
                    credential,
                    courseId,
                    courseWorkId,
                    submissionId,
                    attachment,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && ex is not SubmissionException)
        {
            throw MapClassroomError(ex);
        }
    }

    public async Task<AssignmentSubmission> AddFileAsync(
        string courseId,
        string courseWorkId,
        string submissionId,
        SubmissionFile file,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(file.Path))
            {
                throw new SubmissionException(
                    SubmissionFailureKind.DriveUploadFailed,
                    "Выбранный файл больше не существует.");
            }

            var credential = await _credentialProvider
                .GetCredentialAsync(cancellationToken)
                .ConfigureAwait(false);

            using var drive = CreateDriveService(credential);
            await using var stream = File.OpenRead(file.Path);

            var request = drive.Files.Create(
                new DriveFile { Name = file.Title },
                stream,
                GetMimeType(file.Title));
            request.Fields = "id,name,webViewLink";

            await request.UploadAsync(cancellationToken).ConfigureAwait(false);

            var uploaded = request.ResponseBody;
            if (string.IsNullOrWhiteSpace(uploaded?.Id))
            {
                throw new SubmissionException(
                    SubmissionFailureKind.DriveUploadFailed,
                    "Google Диск не вернул идентификатор загруженного файла.");
            }

            _logger.LogInformation("Uploaded one submission file to Google Drive");

            var attachment = new ApiAttachment
            {
                DriveFile = new ApiDriveFile { Id = uploaded.Id },
            };

            return await ModifyAttachmentsAsync(
                    credential,
                    courseId,
                    courseWorkId,
                    submissionId,
                    attachment,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && ex is not SubmissionException)
        {
            throw MapDriveError(ex);
        }
    }

    public async Task<AssignmentSubmission> TurnInAsync(
        string courseId,
        string courseWorkId,
        string submissionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var credential = await _credentialProvider
                .GetCredentialAsync(cancellationToken)
                .ConfigureAwait(false);

            using var service = CreateClassroomService(credential);
            var request = service.Courses.CourseWork.StudentSubmissions.TurnIn(
                new ApiTurnInRequest(),
                courseId,
                courseWorkId,
                submissionId);

            var updated = await request.ExecuteAsync(cancellationToken).ConfigureAwait(false);

            // The generated TurnIn method returns Empty even though the REST
            // documentation describes a StudentSubmission response. Read the
            // final state explicitly so the UI shows the real status.
            var getRequest = service.Courses.CourseWork.StudentSubmissions.Get(
                courseId,
                courseWorkId,
                submissionId);
            var refreshed = await getRequest.ExecuteAsync(cancellationToken).ConfigureAwait(false);

            return refreshed is null
                ? throw new SubmissionException(
                    SubmissionFailureKind.Unknown,
                    "Google не вернул состояние работы после сдачи.")
                : GoogleClassroomMapper.ToDomain(refreshed);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && ex is not SubmissionException)
        {
            throw MapClassroomError(ex);
        }
    }

    private static async Task<AssignmentSubmission> ModifyAttachmentsAsync(
        ICredential credential,
        string courseId,
        string courseWorkId,
        string submissionId,
        ApiAttachment attachment,
        CancellationToken cancellationToken)
    {
        using var service = CreateClassroomService(credential);
        var body = new ApiModifyAttachmentsRequest
        {
            AddAttachments = [attachment],
        };

        var request = service.Courses.CourseWork.StudentSubmissions.ModifyAttachments(
            body,
            courseId,
            courseWorkId,
            submissionId);

        var updated = await request.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return updated is null
            ? throw new SubmissionException(
                SubmissionFailureKind.Unknown,
                "Google не вернул состояние работы после добавления вложения.")
            : GoogleClassroomMapper.ToDomain(updated);
    }

    private static ClassroomService CreateClassroomService(ICredential credential) =>
        new(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName,
        });

    private static DriveService CreateDriveService(ICredential credential) =>
        new(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName,
        });

    private static SubmissionException MapClassroomError(Exception exception)
    {
        if (exception is GoogleApiException google)
        {
            var status = (int)google.HttpStatusCode;
            return status switch
            {
                401 or 403 => new SubmissionException(
                    SubmissionFailureKind.PermissionDenied,
                    "Google Classroom не разрешил изменить вашу работу через API.",
                    exception),
                404 => new SubmissionException(
                    SubmissionFailureKind.NoSubmission,
                    "Работа не найдена в Google Classroom.",
                    exception),
                429 => new SubmissionException(
                    SubmissionFailureKind.Network,
                    "Google временно ограничил частоту запросов.",
                    exception),
                >= 500 => new SubmissionException(
                    SubmissionFailureKind.Network,
                    "Сервис Google временно недоступен.",
                    exception),
                _ => new SubmissionException(
                    SubmissionFailureKind.Unknown,
                    "Google отклонил запрос на сдачу работы.",
                    exception),
            };
        }

        if (exception is HttpRequestException or TaskCanceledException)
        {
            return new SubmissionException(
                SubmissionFailureKind.Network,
                "Не удалось связаться с Google. Проверьте подключение к интернету.",
                exception);
        }

        return new SubmissionException(
            SubmissionFailureKind.Unknown,
            "Не удалось сдать работу через API.",
            exception);
    }

    private static SubmissionException MapDriveError(Exception exception)
    {
        if (exception is GoogleApiException google)
        {
            var status = (int)google.HttpStatusCode;
            return status switch
            {
                401 or 403 => new SubmissionException(
                    SubmissionFailureKind.DriveUploadFailed,
                    "Google Диск не разрешил загрузить файл. Проверьте, что Drive API включён в проекте.",
                    exception),
                429 => new SubmissionException(
                    SubmissionFailureKind.Network,
                    "Google временно ограничил частоту загрузок.",
                    exception),
                >= 500 => new SubmissionException(
                    SubmissionFailureKind.Network,
                    "Google Диск временно недоступен.",
                    exception),
                _ => new SubmissionException(
                    SubmissionFailureKind.DriveUploadFailed,
                    "Google Диск отклонил загрузку файла.",
                    exception),
            };
        }

        if (exception is IOException or UnauthorizedAccessException)
        {
            return new SubmissionException(
                SubmissionFailureKind.DriveUploadFailed,
                "Не удалось прочитать выбранный файл.",
                exception);
        }

        if (exception is HttpRequestException or TaskCanceledException)
        {
            return new SubmissionException(
                SubmissionFailureKind.Network,
                "Не удалось связаться с Google Диском. Проверьте подключение к интернету.",
                exception);
        }

        return new SubmissionException(
            SubmissionFailureKind.DriveUploadFailed,
            "Не удалось загрузить файл в Google Диск.",
            exception);
    }

    private static string GetMimeType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".csv" => "text/csv",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".zip" => "application/zip",
            ".rar" => "application/vnd.rar",
            ".7z" => "application/x-7z-compressed",
            ".mp4" => "video/mp4",
            ".mp3" => "audio/mpeg",
            _ => "application/octet-stream",
        };
}
