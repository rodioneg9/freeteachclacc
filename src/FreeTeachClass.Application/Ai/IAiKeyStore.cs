using FreeTeachClass.Domain.Enums;

namespace FreeTeachClass.Application.Ai;

/// <summary>
/// Secure storage for user-supplied AI API keys. Implementations must encrypt
/// keys at rest, must never write them to logs and must never return them in
/// diagnostic messages.
/// </summary>
public interface IAiKeyStore
{
    Task<bool> HasKeyAsync(AiProviderKind provider, CancellationToken cancellationToken);

    Task<string?> GetKeyAsync(AiProviderKind provider, CancellationToken cancellationToken);

    Task SaveKeyAsync(AiProviderKind provider, string apiKey, CancellationToken cancellationToken);

    Task DeleteKeyAsync(AiProviderKind provider, CancellationToken cancellationToken);
}
