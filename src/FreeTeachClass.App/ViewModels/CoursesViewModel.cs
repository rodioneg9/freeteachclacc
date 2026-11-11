using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FreeTeachClass.Application.Services;
using FreeTeachClass.Domain.Abstractions;
using Microsoft.Extensions.Logging;

namespace FreeTeachClass.App.ViewModels;

public sealed class CourseItemViewModel
{
    public required string Name { get; init; }
    public required string Subtitle { get; init; }
    public required int AssignmentCount { get; init; }

    public string AssignmentCountText => AssignmentCount switch
    {
        0 => "нет заданий",
        1 => "1 задание",
        >= 2 and <= 4 => $"{AssignmentCount} задания",
        _ => $"{AssignmentCount} заданий",
    };
}

/// <summary>Subjects (courses) list, read from the local cache.</summary>
public sealed partial class CoursesViewModel : ObservableObject
{
    private readonly ILocalDataStore _store;
    private readonly IClassroomSyncService _sync;
    private readonly ILogger<CoursesViewModel> _logger;

    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial string? StatusMessage { get; set; }
    [ObservableProperty] public partial bool IsEmpty { get; set; }

    public ObservableCollection<CourseItemViewModel> Courses { get; } = [];
    public CoursesViewModel(
        ILocalDataStore store,
        IClassroomSyncService sync,
        ILogger<CoursesViewModel> logger)
    {
        _store = store;
        _sync = sync;
        _logger = logger;
    }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var courses = await _store.LoadCoursesAsync(cancellationToken).ConfigureAwait(true);
        var assignments = await _store.LoadAssignmentsAsync(cancellationToken).ConfigureAwait(true);

        var countsPerCourse = assignments
            .GroupBy(a => a.CourseId)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        Courses.Clear();
        foreach (var course in courses.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var parts = new List<string>(3);
            if (!string.IsNullOrEmpty(course.Section))
            {
                parts.Add(course.Section);
            }

            if (!string.IsNullOrEmpty(course.Room))
            {
                parts.Add(course.Room);
            }

            Courses.Add(new CourseItemViewModel
            {
                Name = course.Name,
                Subtitle = parts.Count > 0 ? string.Join(" · ", parts) : "предмет",
                AssignmentCount = countsPerCourse.GetValueOrDefault(course.Id),
            });
        }

        IsEmpty = Courses.Count == 0;
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            await _sync.SyncAsync(cancellationToken).ConfigureAwait(true);
            await LoadAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Courses refresh failed");
            StatusMessage = ErrorMessages.For(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
