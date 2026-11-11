namespace FreeTeachClass.Application.Ai;

/// <summary>
/// One provider adapter. Implementations translate the provider-neutral
/// request into the provider's HTTP API and never log the API key.
/// </summary>
public interface IAiChatClient
{
    Domain.Enums.AiProviderKind Kind { get; }

    Task<AiChatResponse> CompleteAsync(
        AiSettings settings,
        string apiKey,
        AiChatRequest request,
        CancellationToken cancellationToken);
}
