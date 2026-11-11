using FreeTeachClass.Domain.Entities;

namespace FreeTeachClass.Application.Abstractions;

/// <summary>
/// Port to the OAuth 2.0 + account-info part of Google. Implemented in
/// Infrastructure so the Application layer never sees Google types.
/// </summary>
public interface IGoogleAuthGateway
{
    /// <summary>Runs the interactive OAuth consent flow (system browser + loopback redirect).</summary>
    Task<UserProfile> SignInInteractivelyAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Tries to reuse the stored refresh token silently.
    /// Returns null when there is no usable session (user must sign in again).
    /// </summary>
    Task<UserProfile?> RestoreSessionAsync(CancellationToken cancellationToken);

    /// <summary>Revokes the Google grant (best effort) and clears the stored tokens.</summary>
    Task SignOutAsync(CancellationToken cancellationToken);
}
