using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FreeTeachClass.Application.Services;
using FreeTeachClass.Domain.Abstractions;
using Microsoft.Extensions.Logging;

namespace FreeTeachClass.App.ViewModels;

public sealed class CourseFilterItem
{
    public string? CourseId { get; init; }
    public required string Name { get; init; }
}

/// <summary>Assignments list with a per-course filter, sorted by deadline.</summary>
public sealed partial class AssignmentsViewModel : ObservableObject
{
    private readonly ILocalDataStore _store;
    private readonly IClassroomSyncService _sync;
    private readonly AssignmentDetailViewModel _detail;
    private readonly ILogger<AssignmentsViewModel> _logger;

    private List<AssignmentItemViewModel> _allItems = [];

    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial string? StatusMessage { get; set; }
    [ObservableProperty] public partial bool IsEmpty { get; set; }
    [ObservableProperty] public partial CourseFilterItem? SelectedCourseFilter { get; set; }

    public ObservableCollection<CourseFilterItem> CourseFilters { get; } = [];
    public ObservableCollection<AssignmentItemViewModel> Assignments { get; } = [];

    public AssignmentsViewModel(
        ILocalDataStore store,
        IClassroomSyncService sync,
        AssignmentDetailViewModel detail,
        ILogger<AssignmentsViewModel> logger)
    {
        _store = store;
        _sync = sync;
        _detail = detail;
        _logger = logger;
    }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var courses = await _store.LoadCoursesAsync(cancellationToken).ConfigureAwait(true);
        var assignments = await _store.LoadAssignmentsAsync(cancellationToken).ConfigureAwait(true);

        var courseNames = courses.ToDictionary(c => c.Id, c => c.Name, StringComparer.Ordinal);
        var previousFilter = SelectedCourseFilter?.CourseId;

        _allItems = assignments
            .Select(a => AssignmentItemFactory.Create(a, courseNames))
            .OrderBy(a => a.IsOverdue ? 0 : 1)
            .ThenBy(a => a.DueAtSortable)
            .ThenBy(a => a.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        // Every item opens its own full view.
        foreach (var item in _allItems)
        {
            item.OpenCommand = new RelayCommand(() => _detail.Open(item));
        }

        CourseFilters.Clear();
        CourseFilters.Add(new CourseFilterItem { CourseId = null, Name = "Все курсы" });
        foreach (var course in courses.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            CourseFilters.Add(new CourseFilterItem { CourseId = course.Id, Name = course.Name });
        }

        SelectedCourseFilter = CourseFilters.FirstOrDefault(f => f.CourseId == previousFilter)
                               ?? CourseFilters.FirstOrDefault();
    }

    partial void OnSelectedCourseFilterChanged(CourseFilterItem? value) => ApplyFilter();

    private void ApplyFilter()
    {
        Assignments.Clear();

        var filtered = SelectedCourseFilter?.CourseId is { } courseId
            ? _allItems.Where(a => a.CourseId == courseId)
            : _allItems;

        foreach (var item in filtered)
        {
            Assignments.Add(item);
        }

        IsEmpty = Assignments.Count == 0;
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
            _logger.LogWarning(ex, "Assignments refresh failed");
            StatusMessage = ErrorMessages.For(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
