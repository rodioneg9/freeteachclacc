namespace FreeTeachClass.Application.Ai;

/// <summary>Optional assignment context attached to an AI conversation.</summary>
public sealed record AiChatContext(
    string Title,
    string? CourseName,
    string? DueText,
    string? Description,
    IReadOnlyList<string> Materials);
