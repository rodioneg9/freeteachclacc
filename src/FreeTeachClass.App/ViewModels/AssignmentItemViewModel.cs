using System.Windows.Input;
using FreeTeachClass.Domain.Entities;
using FreeTeachClass.Domain.Enums;

namespace FreeTeachClass.App.ViewModels;

/// <summary>An attachment (material) of an assignment.</summary>
public sealed class AssignmentMaterialViewModel
{
    /// <summary>What the user sees: the material name, or its URL as a fallback.</summary>
    public required string Title { get; init; }

    /// <summary>Short kind label ("Ссылка", "Файл Google Диска", …).</summary>
    public required string KindText { get; init; }

    /// <summary>Link to open in the browser; null when the material has none.</summary>
    public string? Url { get; init; }

    /// <summary>The same link as a URI, ready to be opened by a hyperlink.</summary>
    public Uri? LinkUri { get; init; }

    /// <summary>Host of the link ("drive.google.com"), shown as a caption.</summary>
    public string? HostText { get; init; }

    public bool IsLink => LinkUri is not null;
}

/// <summary>One assignment in the list and in the detail view.</summary>
public sealed class AssignmentItemViewModel
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string CourseName { get; init; }
    public required string DueText { get; init; }
    public required string CourseId { get; init; }
    public required DateTimeOffset DueAtSortable { get; init; }
    public required bool IsOverdue { get; init; }

    /// <summary>The domain object this item was built from.</summary>
    public required Assignment Source { get; init; }

    /// <summary>Link to the assignment in the Classroom web UI, when Google provides one.</summary>
    public Uri? ClassroomUri =>
        Uri.TryCreate(Source.AlternateLink, UriKind.Absolute, out var uri) ? uri : null;

    public bool HasClassroomLink => ClassroomUri is not null;

    /// <summary>Full description as Google returned it.</summary>
    public string? Description { get; init; }

    /// <summary>Shortened description for the list.</summary>
    public string? DescriptionPreview { get; init; }

    /// <summary>Attachments of the assignment.</summary>
    public IReadOnlyList<AssignmentMaterialViewModel> Materials { get; init; } = [];

    /// <summary>"3 материала" — a short summary for the list.</summary>
    public string? MaterialsText { get; init; }

    /// <summary>"Задание", "Вопрос с вариантами ответа", …</summary>
    public string? WorkTypeText { get; init; }

    /// <summary>Maximum points, when the teacher set them.</summary>
    public string? MaxPointsText { get; init; }

    /// <summary>
    /// Opens the full view of this assignment. Set by the view model that owns
    /// the list — the item itself knows what it is.
    /// </summary>
    public ICommand? OpenCommand { get; set; }

    public bool IsUrgent => IsOverdue;
    public bool IsNotUrgent => !IsOverdue;
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
    public bool HasMaterials => Materials.Count > 0;
}

/// <summary>
/// Builds the view models of an assignment from the domain entity. Shared by
/// the assignments list and the dashboard, so both show the same data.
/// </summary>
public static class AssignmentItemFactory
{
    public static AssignmentItemViewModel Create(
        Assignment assignment,
        IReadOnlyDictionary<string, string> courseNames)
    {
        var dueAt = assignment.DueAt;
        var isOverdue = dueAt is not null
                        && dueAt.Value < DateTimeOffset.Now
                        && assignment.State == AssignmentState.Published;

        var description = string.IsNullOrWhiteSpace(assignment.Description)
            ? null
            : assignment.Description;

        var preview = description;
        if (preview is { Length: > 120 })
        {
            preview = preview[..120] + "…";
        }

        var materials = assignment.Materials.Select(CreateMaterial).ToArray();

        return new AssignmentItemViewModel
        {
            Id = assignment.Id,
            Source = assignment,
            Title = assignment.Title,
            CourseName = courseNames.GetValueOrDefault(assignment.CourseId, "—"),
            CourseId = assignment.CourseId,
            DueText = dueAt is null ? "без дедлайна" : DateText.Format(dueAt.Value, DateTimeOffset.Now),
            DueAtSortable = dueAt ?? DateTimeOffset.MaxValue,
            IsOverdue = isOverdue,
            Description = description,
            DescriptionPreview = preview,
            Materials = materials,
            MaterialsText = materials.Length > 0
                ? $"{materials.Length} {MaterialWord(materials.Length)}"
                : null,
            WorkTypeText = WorkTypeText(assignment.WorkType),
            MaxPointsText = MaxPointsText(assignment.MaxPoints),
        };
    }

    private static AssignmentMaterialViewModel CreateMaterial(CourseMaterial material)
    {
        var title = material.Title;
        if (string.IsNullOrWhiteSpace(title))
        {
            title = material.Url ?? "Материал";
        }

        return new AssignmentMaterialViewModel
        {
            Title = title,
            KindText = KindText(material.Kind),
            Url = material.Url,
            LinkUri = ToLinkUri(material.Url),
            HostText = HostText(material.Url),
        };
    }

    /// <summary>Only web links are opened; anything else is shown as plain text.</summary>
    private static Uri? ToLinkUri(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https" or "mailto"))
        {
            return null;
        }

        return uri;
    }

    private static string KindText(CourseMaterialKind kind) => kind switch
    {
        CourseMaterialKind.DriveFile => "Файл Google Диска",
        CourseMaterialKind.Link => "Ссылка",
        CourseMaterialKind.YouTubeVideo => "Видео YouTube",
        CourseMaterialKind.Form => "Форма Google",
        _ => "Материал",
    };

    private static string WorkTypeText(AssignmentWorkType workType) => workType switch
    {
        AssignmentWorkType.Assignment => "Задание",
        AssignmentWorkType.ShortAnswerQuestion => "Вопрос с кратким ответом",
        AssignmentWorkType.MultipleChoiceQuestion => "Вопрос с вариантами ответа",
        _ => "Работа",
    };

    private static string? HostText(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return uri.Host;
    }

    private static string? MaxPointsText(double? points) => points is { } value
        ? $"{value:0.##} {PointsWord(value)}"
        : null;

    private static string PointsWord(double points)
    {
        // Fractional scores always take the genitive singular ("2,5 балла").
        if (points != Math.Floor(points))
        {
            return "балла";
        }

        return Plural((int)points, "балл", "балла", "баллов");
    }

    private static string MaterialWord(int count) => Plural(count, "материал", "материала", "материалов");

    /// <summary>Russian plural form for "1/2/5" style nouns.</summary>
    private static string Plural(int count, string one, string few, string many)
    {
        var absolute = Math.Abs(count);
        if (absolute % 100 is >= 11 and <= 14)
        {
            return many;
        }

        return (absolute % 10) switch
        {
            1 => one,
            2 or 3 or 4 => few,
            _ => many,
        };
    }
}
