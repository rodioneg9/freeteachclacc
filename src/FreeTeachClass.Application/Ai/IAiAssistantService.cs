using FreeTeachClass.Domain.Enums;

namespace FreeTeachClass.Application.Ai;

/// <summary>
/// Application-level AI assistant: owns settings/key lookup, prompt assembly
/// and provider selection. The UI never talks to an AI provider directly.
/// </summary>
public interface IAiAssistantService
{
    Task<AiSettings> LoadSettingsAsync(CancellationToken cancellationToken);

    Task SaveSettingsAsync(AiSettings settings, CancellationToken cancellationToken);

    Task<bool> HasKeyAsync(AiProviderKind provider, CancellationToken cancellationToken);

    Task SaveKeyAsync(AiProviderKind provider, string apiKey, CancellationToken cancellationToken);

    Task DeleteKeyAsync(AiProviderKind provider, CancellationToken cancellationToken);

    /// <summary>Sends the user's message with optional assignment context and history.</summary>
    Task<AiChatResponse> SendAsync(
        string userMessage,
        IReadOnlyList<AiChatMessage> history,
        AiChatContext? context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Checks a key by sending a minimal request. When <paramref name="apiKey"/>
    /// is empty, the stored key for the selected provider is used.
    /// </summary>
    Task<string> TestAsync(
        AiSettings settings,
        string? apiKey,
        CancellationToken cancellationToken);
}
