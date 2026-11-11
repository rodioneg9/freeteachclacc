using FreeTeachClass.Application.Abstractions;
using FreeTeachClass.Domain.Abstractions;
using FreeTeachClass.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace FreeTeachClass.Application.Services;

public interface IAuthenticationService
{
    Task<bool> IsSignedInAsync(CancellationToken cancellationToken);
    Task<UserProfile> SignInAsync(CancellationToken cancellationToken);
    Task<UserProfile?> TryRestoreSessionAsync(CancellationToken cancellationToken);
    Task SignOutAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Coordinates the OAuth gateway with the local user cache.
/// </summary>
public sealed class AuthenticationService : IAuthenticationService
{
    private readonly IGoogleAuthGateway _authGateway;
    private readonly ILocalDataStore _localDataStore;
    private readonly ISecureTokenStore _tokenStore;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        IGoogleAuthGateway authGateway,
        ILocalDataStore localDataStore,
        ISecureTokenStore tokenStore,
        ILogger<AuthenticationService> logger)
    {
        _authGateway = authGateway;
        _localDataStore = localDataStore;
        _tokenStore = tokenStore;
        _logger = logger;
    }

    public Task<bool> IsSignedInAsync(CancellationToken cancellationToken) =>
        _tokenStore.HasTokenAsync(cancellationToken);

    public async Task<UserProfile> SignInAsync(CancellationToken cancellationToken)
    {
        var profile = await _authGateway.SignInInteractivelyAsync(cancellationToken).ConfigureAwait(false);
        await _localDataStore.SaveUserAsync(profile, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("User signed in (subject id length: {IdLength})", profile.Id.Length);
        return profile;
    }

    public async Task<UserProfile?> TryRestoreSessionAsync(CancellationToken cancellationToken)
    {
        var profile = await _authGateway.RestoreSessionAsync(cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return null;
        }

        await _localDataStore.SaveUserAsync(profile, cancellationToken).ConfigureAwait(false);
        return profile;
    }

    public async Task SignOutAsync(CancellationToken cancellationToken)
    {
        await _authGateway.SignOutAsync(cancellationToken).ConfigureAwait(false);
        await _localDataStore.ClearAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("User signed out, local cache cleared");
    }
}
