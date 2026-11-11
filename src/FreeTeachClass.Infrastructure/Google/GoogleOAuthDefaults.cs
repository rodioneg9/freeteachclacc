namespace FreeTeachClass.Infrastructure.Google;

/// <summary>
/// OAuth client credentials baked into the source code.
///
/// This is the "paste it right here" variant of the configuration: fill in
/// <see cref="ClientId"/> and <see cref="ClientSecret"/> with the values from
/// Google Cloud Console → APIs &amp; Services → Credentials → OAuth client ID
/// (application type: <b>Desktop app</b>), rebuild the app and sign-in works.
///
/// Priority when the app starts (highest first):
/// <list type="number">
///   <item>environment variables <c>FREETEACHCLASS_GOOGLE_CLIENT_ID</c> / <c>FREETEACHCLASS_GOOGLE_CLIENT_SECRET</c>;</item>
///   <item>file <c>%LOCALAPPDATA%\FreeTeachClass\oauth-client.json</c>;</item>
///   <item>the values below, hardcoded in the code.</item>
/// </list>
///
/// Leaving the fields empty keeps the app in its default "not configured"
/// state: it starts normally and the welcome screen explains how to sign in.
/// Placeholder-looking values (empty, "&lt;client-id&gt;", "PASTE_ME", …) are
/// ignored on purpose, so a half-filled value can never break the sign-in.
/// </summary>
public static class GoogleOAuthDefaults
{
    /// <summary>
    /// OAuth client id of the desktop app, e.g.
    /// <c>1234567890-abcdefghijklmnopqrstuvwxyz012345.apps.googleusercontent.com</c>.
    /// </summary>
    public static string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// OAuth client secret of the desktop app. Optional: desktop clients are
    /// public clients and the PKCE flow works without a secret.
    /// </summary>
    public static string? ClientSecret { get; set; }

    /// <summary>Restores the "not configured" state (used by tests).</summary>
    public static void Reset()
    {
        ClientId = string.Empty;
        ClientSecret = null;
    }
}
