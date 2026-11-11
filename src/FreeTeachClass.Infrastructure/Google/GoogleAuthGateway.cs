using System.Net.Http.Headers;
using System.Text.Json;
using FreeTeachClass.Application.Abstractions;
using FreeTeachClass.Domain.Abstractions;
using FreeTeachClass.Domain.Entities;
using FreeTeachClass.Infrastructure.Storage;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Logging;

namespace FreeTeachClass.Infrastructure.Google;

/// <summary>
/// Source of an authorised Google credential, shared inside Infrastructure
/// between the auth gateway and API data sources.
/// </summary>
public interface IGoogleCredentialProvider
{
    Task<UserCredential> GetCredentialAsync(CancellationToken cancellationToken);
}

/// <summary>
/// OAuth 2.0 for installed apps: system browser + loopback redirect + PKCE,
/// per Google's official guidance for desktop apps. Tokens live only in the
/// secure token store; the userinfo endpoint provides the account profile.
/// </summary>
public sealed class GoogleAuthGateway : IGoogleAuthGateway, IGoogleCredentialProvider
{
    private const string UserInfoUrl = "https://openidconnect.googleapis.com/v1/userinfo";
    private const string RevocationUrl = "https://oauth2.googleapis.com/revoke";

    /// <summary>
    /// Data-store key for the single local user slot. Google's authorization
    /// flow stores the <c>TokenResponse</c> under exactly this key
    /// (<c>IDataStore</c> key = user id passed to the broker), so the secure
    /// token store must read the same key.
    /// </summary>
    public const string FlowUser = "current";

    private static readonly HttpClient Http = new();

    private readonly GoogleOAuthOptions _options;
    private readonly ISecureTokenStore _tokenStore;
    private readonly AuthStateStore _authState;
    private readonly ILogger<GoogleAuthGateway> _logger;

    public GoogleAuthGateway(
        GoogleOAuthOptions options,
        ISecureTokenStore tokenStore,
        AuthStateStore authState,
        ILogger<GoogleAuthGateway> logger)
    {
        _options = options;
        _tokenStore = tokenStore;
        _authState = authState;
        _logger = logger;
    }

    public async Task<UserProfile> SignInInteractivelyAsync(CancellationToken cancellationToken)
    {
        var credential = await AuthorizeAsync(cancellationToken).ConfigureAwait(false);
        return await ReadUserProfileAsync(credential, cancellationToken).ConfigureAwait(false);
    }

    public async Task<UserProfile?> RestoreSessionAsync(CancellationToken cancellationToken)
    {
        if (!await _tokenStore.HasTokenAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        if (!_authState.IsCurrent(GoogleOAuthScopes.CurrentVersion))
        {
            // The app now asks for more permissions than the stored token was
            // issued for. Google cannot upgrade a token silently, so drop it and
            // let the welcome screen run the consent flow again.
            _logger.LogInformation("Stored token was issued for an older scope set; clearing it");
            await _tokenStore.ClearAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        try
        {
            var credential = await AuthorizeAsync(cancellationToken).ConfigureAwait(false);
            return await ReadUserProfileAsync(credential, cancellationToken).ConfigureAwait(false);
        }
        catch (TokenResponseException ex) when (ex.Error is not null)
        {
            // A revoked or expired refresh token: drop the local session and
            // ask the user to sign in again. Only the OAuth error code is logged.
            _logger.LogWarning("Stored session is no longer valid (OAuth error: {ErrorCode})", ex.Error.Error);
            await _tokenStore.ClearAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
    }

    public async Task SignOutAsync(CancellationToken cancellationToken)
    {
        await RevokeRefreshTokenAsync(cancellationToken).ConfigureAwait(false);
        await _tokenStore.ClearAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<UserCredential> GetCredentialAsync(CancellationToken cancellationToken) =>
        AuthorizeAsync(cancellationToken);

    private async Task<UserCredential> AuthorizeAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId))
        {
            throw new MissingOAuthConfigurationException();
        }

        if (_tokenStore is not IDataStore dataStore)
        {
            throw new InvalidOperationException("Хранилище токенов должно реализовывать Google IDataStore.");
        }

        await EnsureScopeVersionAsync(cancellationToken).ConfigureAwait(false);

        var initializer = new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets
            {
                ClientId = _options.ClientId,
                ClientSecret = _options.ClientSecret,
            },
        };

        // usePkce: true — рекомендация Google для installed-приложений.
        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            initializer,
            GoogleOAuthScopes.All,
            FlowUser,
            usePkce: true,
            cancellationToken,
            dataStore,
            codeReceiver: null).ConfigureAwait(false);

        // From now on the stored token covers every scope this app requests.
        _authState.MarkCurrent(GoogleOAuthScopes.CurrentVersion);
        return credential;
    }

    private async Task EnsureScopeVersionAsync(CancellationToken cancellationToken)
    {
        if (_authState.IsCurrent(GoogleOAuthScopes.CurrentVersion))
        {
            return;
        }

        if (await _tokenStore.HasTokenAsync(cancellationToken).ConfigureAwait(false))
        {
            // Clearing forces GoogleWebAuthorizationBroker to run the consent
            // flow instead of silently reusing a token with fewer scopes.
            _logger.LogInformation("Clearing token issued for an older scope set");
            await _tokenStore.ClearAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<UserProfile> ReadUserProfileAsync(
        ICredential credential,
        CancellationToken cancellationToken)
    {
        var accessToken = await credential
            .GetAccessTokenForRequestAsync(UserInfoUrl, cancellationToken)
            .ConfigureAwait(false);

        using var request = new HttpRequestMessage(HttpMethod.Get, UserInfoUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;

        var id = root.TryGetProperty("sub", out var sub) ? sub.GetString() ?? string.Empty : string.Empty;
        var name = root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
        var email = root.TryGetProperty("email", out var emailElement) ? emailElement.GetString() : null;
        var picture = root.TryGetProperty("picture", out var pictureElement) ? pictureElement.GetString() : null;

        if (string.IsNullOrEmpty(id))
        {
            throw new InvalidOperationException("Userinfo-эндпоинт не вернул идентификатор пользователя (sub).");
        }

        return new UserProfile(id, string.IsNullOrWhiteSpace(name) ? id : name, email, picture);
    }

    private async Task RevokeRefreshTokenAsync(CancellationToken cancellationToken)
    {
        if (_tokenStore is not IDataStore dataStore)
        {
            return;
        }

        try
        {
            var token = await dataStore
                .GetAsync<TokenResponse>(FlowUser)
                .ConfigureAwait(false);

            var refreshToken = token?.RefreshToken;
            if (string.IsNullOrEmpty(refreshToken))
            {
                return;
            }

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["token"] = refreshToken,
            });

            // Best effort: revocation may fail offline; local tokens are cleared anyway.
            using var response = await Http
                .PostAsync(RevocationUrl, content, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation("Token revocation completed with status {StatusCode}", response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Token revocation failed; clearing local tokens anyway");
        }
    }
}
