using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FreeTeachClass.Application.Submissions;
using FreeTeachClass.Domain.Entities;
using FreeTeachClass.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace FreeTeachClass.App.ViewModels;

/// <summary>One local file queued for submission.</summary>
public sealed class SubmissionFileViewModel
{
    public required string Path { get; init; }

    public required string Title { get; init; }

    public required string SizeText { get; init; }
}

/// <summary>
/// Full view of one assignment: description, attachments, meta data and the
/// API submission form. Submission status is read from Google when the page is
/// shown; the page itself never calls the submission gateway.
/// </summary>
public sealed partial class AssignmentDetailViewModel : ObservableObject
{
    private const int MaxAttachments = 20;

    private readonly INavigator _navigator;
    private readonly IAssignmentSubmissionService _submissions;
    private readonly AssistantViewModel _assistant;
    private readonly ILogger<AssignmentDetailViewModel> _logger;

    private AssignmentItemViewModel? _item;
    private AssignmentSubmission? _submission;

    [ObservableProperty] public partial string Title { get; set; } = string.Empty;
    [ObservableProperty] public partial string CourseName { get; set; } = string.Empty;
    [ObservableProperty] public partial string DueText { get; set; } = string.Empty;
    [ObservableProperty] public partial string? Description { get; set; }
    [ObservableProperty] public partial string? WorkTypeText { get; set; }
    [ObservableProperty] public partial string? MaxPointsText { get; set; }

    [ObservableProperty] public partial string SubmissionStatusText { get; set; } = string.Empty;
    [ObservableProperty] public partial string SubmissionLink { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsSubmissionBusy { get; set; }
    [ObservableProperty] public partial string? SubmissionWarning { get; set; }
    [ObservableProperty] public partial bool ShowWebFallback { get; set; }

    public ObservableCollection<AssignmentMaterialViewModel> Materials { get; } = [];

    public ObservableCollection<AssignmentMaterialViewModel> SubmissionAttachments { get; } = [];

    public ObservableCollection<SubmissionFileViewModel> SelectedFiles { get; } = [];

    /// <summary>False until an assignment is opened.</summary>
    public bool HasAssignment { get; private set; }

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public bool HasMaterials => Materials.Count > 0;

    public bool IsApiSubmissionSupported =>
        _item?.Source.WorkType == AssignmentWorkType.Assignment;

    public bool IsSubmissionLoaded => _submission is not null;

    public bool IsTurnedIn => _submission?.State == SubmissionState.TurnedIn;

    public bool IsReturned => _submission?.State == SubmissionState.Returned;

    public bool HasClassroomLink => _item?.HasClassroomLink == true;

    public Uri? ClassroomUri => _item?.ClassroomUri;

    public bool HasSelectedFiles => SelectedFiles.Count > 0;

    public bool HasExistingAttachments => SubmissionAttachments.Count > 0;

    public bool HasSubmissionInput =>
        HasSelectedFiles
        || HasExistingAttachments
        || !string.IsNullOrWhiteSpace(SubmissionLink);

    public bool HasSubmissionWarning => !string.IsNullOrWhiteSpace(SubmissionWarning);

    public bool ShowSubmissionForm =>
        HasAssignment && IsApiSubmissionSupported && IsSubmissionLoaded && !IsTurnedIn;

    public bool CanAddFiles => ShowSubmissionForm && !IsSubmissionBusy;

    public bool CanSubmit => CanAddFiles && HasSubmissionInput;

    public AssignmentDetailViewModel(
        INavigator navigator,
        IAssignmentSubmissionService submissions,
        AssistantViewModel assistant,
        ILogger<AssignmentDetailViewModel> logger)
    {
        _navigator = navigator;
        _submissions = submissions;
        _assistant = assistant;
        _logger = logger;

        SelectedFiles.CollectionChanged += OnSelectedFilesChanged;
    }

    /// <summary>Fills the view with the data of the chosen assignment.</summary>
    public void Show(AssignmentItemViewModel item)
    {
        _item = item;
        _submission = null;

        Title = item.Title;
        CourseName = item.CourseName;
        DueText = item.DueText;
        Description = item.Description;
        WorkTypeText = item.WorkTypeText;
        MaxPointsText = item.MaxPointsText;

        Materials.Clear();
        foreach (var material in item.Materials)
        {
            Materials.Add(material);
        }

        SubmissionAttachments.Clear();
        SelectedFiles.Clear();
        SubmissionLink = string.Empty;
        SubmissionWarning = null;
        ShowWebFallback = false;
        SubmissionStatusText = "Проверяем статус работы…";

        HasAssignment = true;
        NotifyAssignmentChanged();
        NotifySubmissionChanged();

        if (!IsApiSubmissionSupported)
        {
            SubmissionWarning =
                "Через API можно сдавать только задания типа «Задание». " +
                "Для вопросов и тестов откройте задание в Classroom.";
            ShowWebFallback = item.HasClassroomLink;
        }
    }

    partial void OnDescriptionChanged(string? value) => OnPropertyChanged(nameof(HasDescription));

    /// <summary>Shows the assignment and opens its page.</summary>
    public void Open(AssignmentItemViewModel item)
    {
        Show(item);
        _navigator.Navigate(PageKey.AssignmentDetail);
    }

    partial void OnIsSubmissionBusyChanged(bool value) => NotifySubmissionChanged();

    partial void OnSubmissionLinkChanged(string value) => NotifySubmissionChanged();

    partial void OnSubmissionWarningChanged(string? value) =>
        OnPropertyChanged(nameof(HasSubmissionWarning));

    /// <summary>Reads the current submission from Google. Called on page load.</summary>
    public async Task LoadSubmissionAsync(CancellationToken cancellationToken)
    {
        var item = _item;
        if (item is null)
        {
            return;
        }

        try
        {
            var submission = await _submissions
                .GetAsync(item.CourseId, item.Id, cancellationToken)
                .ConfigureAwait(true);

            if (!ReferenceEquals(_item, item))
            {
                return;
            }

            ApplySubmission(submission);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load submission state");
            if (ReferenceEquals(_item, item))
            {
                SubmissionStatusText = "Не удалось получить статус работы.";
                SubmissionWarning = ErrorMessages.For(ex);
                ShowWebFallback = item.HasClassroomLink;
            }
        }
    }

    /// <summary>Adds picked files, de-duplicating paths and respecting the API limit.</summary>
    public void AddFiles(IEnumerable<string> paths)
    {
        if (!CanAddFiles)
        {
            return;
        }

        foreach (var path in paths)
        {
            if (SelectedFiles.Count + SubmissionAttachments.Count >= MaxAttachments)
            {
                SubmissionWarning =
                    $"К работе можно прикрепить не больше {MaxAttachments} вложений.";
                ShowWebFallback = _item?.HasClassroomLink == true;
                return;
            }

            if (SelectedFiles.Any(file => string.Equals(file.Path, path, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            try
            {
                var info = new FileInfo(path);
                if (!info.Exists)
                {
                    continue;
                }

                SelectedFiles.Add(new SubmissionFileViewModel
                {
                    Path = path,
                    Title = string.IsNullOrWhiteSpace(info.Name) ? "Файл" : info.Name,
                    SizeText = FormatSize(info.Length),
                });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not read a picked file");
                SubmissionWarning = "Не удалось прочитать выбранный файл.";
            }
        }
    }

    public void RemoveFile(string? path)
    {
        if (path is null)
        {
            return;
        }

        var file = SelectedFiles.FirstOrDefault(
            item => string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase));
        if (file is not null)
        {
            SelectedFiles.Remove(file);
        }
    }

    /// <summary>
    /// Uploads/attaches the selected work and turns it in. The Classroom API
    /// only permits this for coursework created by the same Cloud project; any
    /// failure is turned into a warning with a deep link to the web UI.
    /// </summary>
    public async Task SubmitAsync(CancellationToken cancellationToken = default)
    {
        var item = _item;
        if (item is null || !CanSubmit)
        {
            return;
        }

        IsSubmissionBusy = true;
        SubmissionWarning = null;
        ShowWebFallback = false;

        try
        {
            var files = SelectedFiles
                .Select(file => new SubmissionFile(file.Path, file.Title))
                .ToArray();

            var outcome = await _submissions
                .SubmitAsync(item.Source, files, SubmissionLink, cancellationToken)
                .ConfigureAwait(true);

            SelectedFiles.Clear();
            SubmissionLink = string.Empty;
            ApplySubmission(outcome.Submission);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SubmissionException ex)
        {
            _logger.LogWarning(
                "Submission failed ({Kind}) for course {CourseId}, coursework {CourseWorkId}",
                ex.Kind,
                item.CourseId,
                item.Id);
            SubmissionWarning = DescribeFailure(ex);
            ShowWebFallback = item.HasClassroomLink && ShouldOfferWebFallback(ex.Kind);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected submission error");
            SubmissionWarning = ErrorMessages.For(ex);
            ShowWebFallback = item.HasClassroomLink;
        }
        finally
        {
            IsSubmissionBusy = false;
        }
    }

    [RelayCommand]
    private void AskAssistant()
    {
        if (_item is null)
        {
            return;
        }

        _assistant.SetContext(_item);
        _navigator.Navigate(PageKey.Assistant);
    }

    [RelayCommand]
    private void Back() => _navigator.Navigate(PageKey.Assignments);

    private void ApplySubmission(AssignmentSubmission? submission)
    {
        _submission = submission;

        SubmissionAttachments.Clear();
        if (submission is not null)
        {
            foreach (var attachment in submission.Attachments)
            {
                SubmissionAttachments.Add(ToViewModel(attachment));
            }
        }

        SubmissionStatusText = DescribeState(submission);

        if (submission is null)
        {
            SubmissionWarning =
                "Google не вернул вашу работу для этого задания. " +
                "Откройте задание в Classroom и попробуйте ещё раз.";
            ShowWebFallback = _item?.HasClassroomLink == true;
        }

        NotifySubmissionChanged();
    }

    private void NotifyAssignmentChanged()
    {
        OnPropertyChanged(nameof(HasAssignment));
        OnPropertyChanged(nameof(HasDescription));
        OnPropertyChanged(nameof(HasMaterials));
    }

    private void NotifySubmissionChanged()
    {
        OnPropertyChanged(nameof(IsApiSubmissionSupported));
        OnPropertyChanged(nameof(IsSubmissionLoaded));
        OnPropertyChanged(nameof(IsTurnedIn));
        OnPropertyChanged(nameof(IsReturned));
        OnPropertyChanged(nameof(HasClassroomLink));
        OnPropertyChanged(nameof(ClassroomUri));
        OnPropertyChanged(nameof(HasSelectedFiles));
        OnPropertyChanged(nameof(HasExistingAttachments));
        OnPropertyChanged(nameof(HasSubmissionInput));
        OnPropertyChanged(nameof(ShowSubmissionForm));
        OnPropertyChanged(nameof(CanAddFiles));
        OnPropertyChanged(nameof(CanSubmit));
    }

    private void OnSelectedFilesChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        NotifySubmissionChanged();

    private static string DescribeState(AssignmentSubmission? submission)
    {
        if (submission is null)
        {
            return "Работа не найдена.";
        }

        var updated = submission.UpdateTime == DateTimeOffset.UnixEpoch
            ? null
            : submission.UpdateTime.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture);

        return submission.State switch
        {
            SubmissionState.TurnedIn =>
                "Сдано" + (updated is null ? string.Empty : $" {updated}")
                + (submission.IsLate ? " · с опозданием" : string.Empty)
                + GradeText(submission),
            SubmissionState.Returned =>
                "Возвращено преподавателем" + GradeText(submission),
            SubmissionState.Reclaimed =>
                "Возвращено на доработку" + (updated is null ? string.Empty : $" {updated}"),
            _ =>
                "Не сдано" + (updated is null ? string.Empty : $" · обновлено {updated}"),
        };
    }

    private static string GradeText(AssignmentSubmission submission)
    {
        if (submission.AssignedGrade is { } grade)
        {
            return $" · оценка {grade:0.##}";
        }

        if (submission.DraftGrade is { } draft)
        {
            return $" · черновик оценки {draft:0.##}";
        }

        return string.Empty;
    }

    private static string DescribeFailure(SubmissionException exception) => exception.Kind switch
    {
        SubmissionFailureKind.PermissionDenied =>
            "Google Classroom не разрешил сдать работу через API. Это ограничение Google: " +
            "изменять и сдавать задание может только приложение, создавшее его. " +
            "Откройте задание в Classroom и прикрепите файлы там.",
        SubmissionFailureKind.DriveUploadFailed =>
            "Не удалось загрузить файл в Google Диск. Проверьте, что Drive API включён " +
            "в вашем проекте Google Cloud, и попробуйте снова. Можно прикрепить файл на сайте Classroom.",
        SubmissionFailureKind.Network =>
            "Не удалось связаться с Google. Проверьте подключение к интернету и попробуйте снова. " +
            "Можно открыть задание в Classroom.",
        SubmissionFailureKind.NoSubmission =>
            "Google не вернул вашу работу для этого задания. Откройте его в Classroom и попробуйте снова.",
        SubmissionFailureKind.TooManyAttachments => exception.Message,
        SubmissionFailureKind.InvalidLink => exception.Message,
        SubmissionFailureKind.UnsupportedWorkType => exception.Message,
        _ =>
            "Не удалось сдать работу через API. Откройте задание в Classroom и прикрепите файлы там.",
    };

    private static bool ShouldOfferWebFallback(SubmissionFailureKind kind) => kind switch
    {
        SubmissionFailureKind.PermissionDenied
            or SubmissionFailureKind.DriveUploadFailed
            or SubmissionFailureKind.Network
            or SubmissionFailureKind.NoSubmission
            or SubmissionFailureKind.Unknown => true,
        _ => false,
    };

    private static AssignmentMaterialViewModel ToViewModel(SubmissionAttachment attachment)
    {
        var url = attachment.Url;
        var uri = Uri.TryCreate(url, UriKind.Absolute, out var parsed)
                  && parsed.Scheme is "http" or "https" or "mailto"
            ? parsed
            : null;

        return new AssignmentMaterialViewModel
        {
            Title = attachment.Title ?? url ?? "Вложение",
            KindText = attachment.Kind switch
            {
                CourseMaterialKind.DriveFile => "Файл Google Диска",
                CourseMaterialKind.Link => "Ссылка",
                CourseMaterialKind.YouTubeVideo => "Видео YouTube",
                CourseMaterialKind.Form => "Форма Google",
                _ => "Вложение",
            },
            Url = url,
            LinkUri = uri,
            HostText = uri?.Host,
        };
    }

    private static string FormatSize(long bytes)
    {
        const double kilobyte = 1024;
        const double megabyte = kilobyte * 1024;

        if (bytes >= megabyte)
        {
            return $"{bytes / megabyte:0.#} МБ";
        }

        if (bytes >= kilobyte)
        {
            return $"{bytes / kilobyte:0.#} КБ";
        }

        return $"{bytes} Б";
    }
}
