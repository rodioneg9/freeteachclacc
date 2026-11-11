using FreeTeachClass.Application.Abstractions;
using FreeTeachClass.Application.Submissions;
using FreeTeachClass.Domain.Entities;
using FreeTeachClass.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace FreeTeachClass.Application.Tests;

public sealed class AssignmentSubmissionServiceTests
{
    [Fact]
    public async Task Submit_AddsLinkAndFileThenTurnsIn()
    {
        var gateway = new FakeSubmissionGateway(NewSubmission());
        var service = CreateService(gateway);

        var outcome = await service.SubmitAsync(
            TestData.Assignment(),
            [new SubmissionFile(@"C:\work\essay.pdf", "essay.pdf")],
            "https://example.com/essay",
            CancellationToken.None);

        Assert.Equal(["Get", "AddLink", "AddFile", "TurnIn"], gateway.Calls);
        Assert.Equal(2, outcome.AddedAttachments);
        Assert.True(outcome.TurnedIn);
        Assert.Equal(SubmissionState.TurnedIn, outcome.Submission.State);
        Assert.Equal(2, outcome.Submission.Attachments.Count);
        Assert.Equal("example.com", gateway.LastLinkTitle);
        Assert.Equal("essay.pdf", gateway.LastFileName);
    }

    [Fact]
    public async Task Submit_SkipsAttachmentsThatAreAlreadyPresent()
    {
        var gateway = new FakeSubmissionGateway(NewSubmission(
            new SubmissionAttachment(CourseMaterialKind.Link, null, "example.com", "https://example.com/essay"),
            new SubmissionAttachment(CourseMaterialKind.DriveFile, "drive-1", "essay.pdf", null)));
        var service = CreateService(gateway);

        var outcome = await service.SubmitAsync(
            TestData.Assignment(),
            [new SubmissionFile(@"C:\work\essay.pdf", "essay.pdf")],
            "https://example.com/essay",
            CancellationToken.None);

        Assert.Equal(["Get", "TurnIn"], gateway.Calls);
        Assert.Equal(0, outcome.AddedAttachments);
        Assert.True(outcome.TurnedIn);
    }

    [Fact]
    public async Task Submit_AlreadyTurnedIn_DoesNotModifyOrTurnInAgain()
    {
        var gateway = new FakeSubmissionGateway(NewSubmission(SubmissionState.TurnedIn));
        var service = CreateService(gateway);

        var outcome = await service.SubmitAsync(
            TestData.Assignment(),
            [],
            linkUrl: null,
            CancellationToken.None);

        Assert.Equal(["Get"], gateway.Calls);
        Assert.Equal(0, outcome.AddedAttachments);
        Assert.True(outcome.TurnedIn);
    }

    [Fact]
    public async Task Submit_UnsupportedWorkType_ThrowsBeforeCallingGoogle()
    {
        var gateway = new FakeSubmissionGateway(NewSubmission());
        var service = CreateService(gateway);
        var assignment = TestData.Assignment() with
        {
            WorkType = AssignmentWorkType.ShortAnswerQuestion,
        };

        var exception = await Assert.ThrowsAsync<SubmissionException>(() =>
            service.SubmitAsync(assignment, [], linkUrl: null, CancellationToken.None));

        Assert.Equal(SubmissionFailureKind.UnsupportedWorkType, exception.Kind);
        Assert.Empty(gateway.Calls);
    }

    [Theory]
    [InlineData("not a link")]
    [InlineData("ftp://example.com/file")]
    [InlineData("javascript:alert(1)")]
    public async Task Submit_InvalidLink_ThrowsBeforeCallingGoogle(string link)
    {
        var gateway = new FakeSubmissionGateway(NewSubmission());
        var service = CreateService(gateway);

        var exception = await Assert.ThrowsAsync<SubmissionException>(() =>
            service.SubmitAsync(TestData.Assignment(), [], link, CancellationToken.None));

        Assert.Equal(SubmissionFailureKind.InvalidLink, exception.Kind);
        Assert.Empty(gateway.Calls);
    }

    [Fact]
    public async Task Submit_MissingSubmission_ThrowsNoSubmission()
    {
        var gateway = new FakeSubmissionGateway(submission: null);
        var service = CreateService(gateway);

        var exception = await Assert.ThrowsAsync<SubmissionException>(() =>
            service.SubmitAsync(TestData.Assignment(), [], linkUrl: null, CancellationToken.None));

        Assert.Equal(SubmissionFailureKind.NoSubmission, exception.Kind);
        Assert.Equal(["Get"], gateway.Calls);
    }

    [Fact]
    public async Task Submit_AtAttachmentLimit_ThrowsWithoutCallingAttachOrTurnIn()
    {
        var attachments = Enumerable
            .Range(0, 20)
            .Select(index => new SubmissionAttachment(
                CourseMaterialKind.Link,
                null,
                $"link-{index}",
                $"https://example.com/{index}"))
            .ToArray();
        var gateway = new FakeSubmissionGateway(NewSubmission(attachments));
        var service = CreateService(gateway);

        var exception = await Assert.ThrowsAsync<SubmissionException>(() =>
            service.SubmitAsync(
                TestData.Assignment(),
                [new SubmissionFile(@"C:\work\essay.pdf", "essay.pdf")],
                linkUrl: null,
                CancellationToken.None));

        Assert.Equal(SubmissionFailureKind.TooManyAttachments, exception.Kind);
        Assert.DoesNotContain("AddFile", gateway.Calls);
        Assert.DoesNotContain("TurnIn", gateway.Calls);
    }

    [Fact]
    public async Task Submit_SecondFileExceedingLimit_ThrowsAfterFirstFileButBeforeTurnIn()
    {
        var existing = Enumerable
            .Range(0, 19)
            .Select(index => new SubmissionAttachment(
                CourseMaterialKind.Link,
                null,
                $"link-{index}",
                $"https://example.com/{index}"))
            .ToArray();
        var gateway = new FakeSubmissionGateway(NewSubmission(existing));
        var service = CreateService(gateway);

        var exception = await Assert.ThrowsAsync<SubmissionException>(() =>
            service.SubmitAsync(
                TestData.Assignment(),
                [
                    new SubmissionFile(@"C:\work\one.pdf", "one.pdf"),
                    new SubmissionFile(@"C:\work\two.pdf", "two.pdf"),
                ],
                linkUrl: null,
                CancellationToken.None));

        Assert.Equal(SubmissionFailureKind.TooManyAttachments, exception.Kind);
        Assert.Single(gateway.Calls.Where(call => call == "AddFile"));
        Assert.DoesNotContain("TurnIn", gateway.Calls);
    }

    [Fact]
    public async Task Submit_WhitespaceLink_IsIgnored()
    {
        var gateway = new FakeSubmissionGateway(NewSubmission());
        var service = CreateService(gateway);

        var outcome = await service.SubmitAsync(
            TestData.Assignment(),
            [new SubmissionFile(@"C:\work\essay.pdf", "essay.pdf")],
            "   ",
            CancellationToken.None);

        Assert.Equal(["Get", "AddFile", "TurnIn"], gateway.Calls);
        Assert.Equal(1, outcome.AddedAttachments);
    }

    private static AssignmentSubmissionService CreateService(FakeSubmissionGateway gateway) =>
        new(gateway, NullLogger<AssignmentSubmissionService>.Instance);

    private static AssignmentSubmission NewSubmission(
        SubmissionState state = SubmissionState.New,
        params SubmissionAttachment[] attachments) =>
        new(
            Id: "submission-1",
            CourseId: "course-1",
            CourseWorkId: "work-1",
            State: state,
            IsLate: false,
            AssignedGrade: null,
            DraftGrade: null,
            UpdateTime: DateTimeOffset.UnixEpoch,
            Attachments: attachments);

    private sealed class FakeSubmissionGateway(AssignmentSubmission? submission) : ISubmissionGateway
    {
        public AssignmentSubmission? Submission { get; private set; } = submission;

        public List<string> Calls { get; } = [];

        public string? LastLinkTitle { get; private set; }

        public string? LastFileName { get; private set; }

        public Task<AssignmentSubmission?> GetStudentSubmissionAsync(
            string courseId,
            string courseWorkId,
            CancellationToken cancellationToken)
        {
            Calls.Add("Get");
            return Task.FromResult(Submission);
        }

        public Task<AssignmentSubmission> AddLinkAsync(
            string courseId,
            string courseWorkId,
            string submissionId,
            string url,
            string title,
            CancellationToken cancellationToken)
        {
            Calls.Add("AddLink");
            LastLinkTitle = title;
            return Task.FromResult(Add(new SubmissionAttachment(
                Kind: CourseMaterialKind.Link,
                DriveFileId: null,
                Title: title,
                Url: url)));
        }

        public Task<AssignmentSubmission> AddFileAsync(
            string courseId,
            string courseWorkId,
            string submissionId,
            SubmissionFile file,
            CancellationToken cancellationToken)
        {
            Calls.Add("AddFile");
            LastFileName = file.Title;
            return Task.FromResult(Add(new SubmissionAttachment(
                Kind: CourseMaterialKind.DriveFile,
                DriveFileId: "drive-1",
                Title: file.Title,
                Url: null)));
        }

        public Task<AssignmentSubmission> TurnInAsync(
            string courseId,
            string courseWorkId,
            string submissionId,
            CancellationToken cancellationToken)
        {
            Calls.Add("TurnIn");
            Submission = Submission! with
            {
                State = SubmissionState.TurnedIn,
                UpdateTime = DateTimeOffset.UtcNow,
            };
            return Task.FromResult(Submission);
        }

        private AssignmentSubmission Add(SubmissionAttachment attachment)
        {
            Submission = Submission! with
            {
                Attachments = [.. Submission.Attachments, attachment],
            };
            return Submission;
        }
    }
}
