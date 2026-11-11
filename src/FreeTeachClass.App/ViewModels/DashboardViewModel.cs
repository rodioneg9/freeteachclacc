using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FreeTeachClass.Application.Services;
using FreeTeachClass.Domain.Abstractions;
using FreeTeachClass.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace FreeTeachClass.App.ViewModels;

public sealed class TodayEntryViewModel
{
    public required string AssignmentId { get; init; }
    public required string Title { get; init; }
    public required string CourseName { get; init; }
    public required string DueText { get; init; }
    public required bool IsOverdue { get; init; }

    /// <summary>Opens the full view of the assignment behind this entry.</summary>
    public ICommand? OpenCommand { get; set; }

    public bool IsUrgent => IsOverdue;
    public bool IsNotUrgent => !IsOverdue;
}

/// <summary>
/// Dashboard: user greeting, counts, nearest deadline, "Сегодня" list and
/// placeholder cards for AI / XP / achievements.
/// </summary>
public sealed partial class DashboardViewModel : ObservableObject
{
    private static readonly TimeSpan AutoSyncAge = TimeSpan.FromMinutes(10);

    private readonly ILocalDataStore _store;
    private readonly IClassroomSyncService _sync;
    private readonly IDashboardService _dashboard;
    private readonly AssignmentDetailViewModel _detail;
    private readonly ILogger<DashboardViewModel> _logger;

    private IReadOnlyList<Assignment> _assignments = [];
    private IReadOnlyDictionary<string, string> _courseNames = new Dictionary<string, string>(StringComparer.Ordinal);

    [ObservableProperty] public partial string UserName { get; set; }
    [ObservableProperty] public partial string CourseCountText { get; set; }
    [ObservableProperty] public partial string ActiveAssignmentCountText { get; set; }
    [ObservableProperty] public partial string NearestDeadlineText { get; set; }
    [ObservableProperty] public partial string LastSyncText { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial string? StatusMessage { get; set; }
    [ObservableProperty] public partial bool HasTodayEntries { get; set; }
    [ObservableProperty] public partial bool HasNoTodayEntries { get; set; }

    public ObservableCollection<TodayEntryViewModel> TodayEntries { get; } = [];

    public DashboardViewModel(
        ILocalDataStore store,
        IClassroomSyncService sync,
        IDashboardService dashboard,
        AssignmentDetailViewModel detail,
        ILogger<DashboardViewModel> logger)
    {
        _store = store;
        _sync = sync;
        _dashboard = dashboard;
        _detail = detail;
        _logger = logger;

        // Defaults shown until the local cache is rendered.
        UserName = "Гость";
        CourseCountText = "—";
        ActiveAssignmentCountText = "—";
        NearestDeadlineText = "—";
        LastSyncText = "ещё не синхронизировано";
    }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        await LoadCoreAsync(forceSync: false, cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await LoadCoreAsync(forceSync: true, cancellationToken).ConfigureAwait(true);
    }

    private async Task LoadCoreAsync(bool forceSync, CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var user = await _store.LoadUserAsync(cancellationToken).ConfigureAwait(true);
            UserName = string.IsNullOrWhiteSpace(user?.Name) ? "Гость" : user!.Name;

            await RenderFromCacheAsync(cancellationToken).ConfigureAwait(true);

            var lastSync = await _store.LoadLastSyncUtcAsync(cancellationToken).ConfigureAwait(true);
            var isStale = lastSync is null || DateTimeOffset.UtcNow - lastSync.Value > AutoSyncAge;

            if (forceSync || isStale)
            {
                try
                {
                    var result = await _sync.SyncAsync(cancellationToken).ConfigureAwait(true);
                    StatusMessage = result.CoursesFailed > 0
                        ? $"Обновлено не всё: не удалось получить {result.CoursesFailed} " +
                          $"из {result.CoursesSynced + result.CoursesFailed} курсов."
                        : null;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Dashboard sync failed");
                    StatusMessage = ErrorMessages.For(ex);
                }

                await RenderFromCacheAsync(cancellationToken).ConfigureAwait(true);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RenderFromCacheAsync(CancellationToken cancellationToken)
    {
        var courses = await _store.LoadCoursesAsync(cancellationToken).ConfigureAwait(true);
        var assignments = await _store.LoadAssignmentsAsync(cancellationToken).ConfigureAwait(true);
        var lastSync = await _store.LoadLastSyncUtcAsync(cancellationToken).ConfigureAwait(true);

        // Kept for the detail view: clicking a "today" entry opens the assignment.
        _assignments = assignments;
        _courseNames = courses.ToDictionary(c => c.Id, c => c.Name, StringComparer.Ordinal);

        var snapshot = _dashboard.BuildSnapshot(courses, assignments, DateTimeOffset.UtcNow);

        CourseCountText = snapshot.CourseCount.ToString();
        ActiveAssignmentCountText = snapshot.ActiveAssignmentCount.ToString();
        NearestDeadlineText = snapshot.NearestDeadline?.DueAt is { } due
            ? DateText.Format(due, DateTimeOffset.Now)
            : "нет";
        LastSyncText = lastSync is null
            ? "ещё не синхронизировано"
            : $"последнее обновление: {lastSync.Value.ToLocalTime():dd.MM.yyyy HH:mm}";

        TodayEntries.Clear();
        foreach (var entry in snapshot.TodayEntries)
        {
            var item = new TodayEntryViewModel
            {
                AssignmentId = entry.AssignmentId,
                Title = entry.Title,
                CourseName = entry.CourseName,
                DueText = DateText.Format(entry.DueAt, DateTimeOffset.Now),
                IsOverdue = entry.IsOverdue,
            };

            // Clicking an entry opens the full view of the assignment.
            var assignment = _assignments.FirstOrDefault(a => a.Id == entry.AssignmentId);
            if (assignment is not null)
            {
                item.OpenCommand = new RelayCommand(() =>
                    _detail.Open(AssignmentItemFactory.Create(assignment, _courseNames)));
            }

            TodayEntries.Add(item);
        }

        HasTodayEntries = TodayEntries.Count > 0;
        HasNoTodayEntries = !HasTodayEntries;
    }
}
