namespace FreeTeachClass.Application.Ai;

/// <summary>Normalised provider response. Token counts are optional.</summary>
public sealed record AiChatResponse(string Text, int? InputTokens = null, int? OutputTokens = null);
