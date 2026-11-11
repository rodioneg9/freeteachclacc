namespace FreeTeachClass.Application.Ai;

/// <summary>Provider-neutral request passed to an <c>IAiChatClient</c>.</summary>
public sealed record AiChatRequest(
    string Model,
    IReadOnlyList<AiChatMessage> Messages,
    string? SystemPrompt,
    double? Temperature,
    int MaxTokens);
