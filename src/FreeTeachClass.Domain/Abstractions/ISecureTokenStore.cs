namespace FreeTeachClass.Domain.Abstractions;

/// <summary>
/// Secure, OS-backed storage for OAuth tokens. Implementations must never
/// write tokens to plain text and must never log their contents.
/// </summary>
public interface ISecureTokenStore
{
    /// <summary>True when a previously stored session exists (a refresh token is present).</summary>
    Task<bool> HasTokenAsync(CancellationToken cancellationToken);

    /// <summary>Deletes the stored session, e.g. on sign-out or revocation.</summary>
    Task ClearAsync(CancellationToken cancellationToken);
}
