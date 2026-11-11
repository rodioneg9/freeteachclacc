using FreeTeachClass.Domain.Abstractions;
using FreeTeachClass.Domain.Entities;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Classroom.v1;
using Google.Apis.Services;
using Microsoft.Extensions.Logging;

namespace FreeTeachClass.Infrastructure.Google;

/// <summary>
/// Calls the official Google Classroom API v1 with the user's credential.
/// Handles pagination; the Application layer decides when to sync.
/// </summary>
public sealed class GoogleClassroomDataSource : IClassroomDataSource
{
    private const int PageSize = 100;
    private const string ApplicationName = "FreeTeachClass";

    private readonly IGoogleCredentialProvider _credentialProvider;
    private readonly ILogger<GoogleClassroomDataSource> _logger;

    public GoogleClassroomDataSource(
        IGoogleCredentialProvider credentialProvider,
        ILogger<GoogleClassroomDataSource> logger)
    {
        _credentialProvider = credentialProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Course>> GetCoursesAsync(CancellationToken cancellationToken)
    {
        var credential = await _credentialProvider.GetCredentialAsync(cancellationToken).ConfigureAwait(false);
        using var service = CreateService(credential);

        var courses = new List<Course>();
        var request = service.Courses.List();
        request.CourseStatesList = new[] { CoursesResource.ListRequest.CourseStatesEnum.ACTIVE };
        request.PageSize = PageSize;

        do
        {
            var page = await request.ExecuteAsync(cancellationToken).ConfigureAwait(false);
            if (page.Courses is not null)
            {
                courses.AddRange(page.Courses.Select(GoogleClassroomMapper.ToDomain));
            }

            request.PageToken = page.NextPageToken;
        }
        while (!string.IsNullOrEmpty(request.PageToken));

        _logger.LogDebug("Fetched {Count} active courses from Classroom", courses.Count);
        return courses;
    }

    public async Task<IReadOnlyList<Assignment>> GetAssignmentsAsync(
        string courseId,
        CancellationToken cancellationToken)
    {
        var credential = await _credentialProvider.GetCredentialAsync(cancellationToken).ConfigureAwait(false);
        using var service = CreateService(credential);

        var assignments = new List<Assignment>();

        // courses.courseWork.list with the read-only "me" scope
        // (classroom.coursework.me.readonly): coursework the signed-in user may
        // see. PUBLISHED is requested server-side — drafts are out of MVP scope
        // and students never receive drafts anyway.
        var request = service.Courses.CourseWork.List(courseId);
        request.CourseWorkStates = CoursesResource.CourseWorkResource.ListRequest.CourseWorkStatesEnum.PUBLISHED;
        request.PageSize = PageSize;

        do
        {
            var page = await request.ExecuteAsync(cancellationToken).ConfigureAwait(false);
            if (page.CourseWork is not null)
            {
                assignments.AddRange(page.CourseWork.Select(work =>
                    GoogleClassroomMapper.ToDomain(work, courseId)));
            }

            request.PageToken = page.NextPageToken;
        }
        while (!string.IsNullOrEmpty(request.PageToken));

        _logger.LogDebug("Fetched {Count} assignments for course {CourseId}", assignments.Count, courseId);
        return assignments;
    }

    private static ClassroomService CreateService(ICredential credential) =>
        new(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName,
        });
}
