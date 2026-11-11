namespace FreeTeachClass.Domain.Enums;

/// <summary>
/// Supported AI providers. <see cref="CustomOpenAiCompatible"/> covers any
/// service that implements the OpenAI chat-completions request shape.
/// </summary>
public enum AiProviderKind
{
    OpenAi,
    OpenRouter,
    Gemini,
    Anthropic,
    CustomOpenAiCompatible,
}
