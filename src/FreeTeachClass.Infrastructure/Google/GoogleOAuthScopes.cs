namespace FreeTeachClass.Infrastructure.Google;

/// <summary>
/// The OAuth scopes required by the MVP, verified against the official Google
/// documentation (OAuth 2.0 Scopes for Google APIs / Classroom REST reference,
/// checked 2026-10):
/// <list type="bullet">
/// <item><c>openid</c>, <c>userinfo.profile</c>, <c>userinfo.email</c> — account info via OpenID Connect userinfo.</item>
/// <item><c>classroom.courses.readonly</c> — list the user's courses (courses.list).</item>
/// <item><c>classroom.coursework.me.readonly</c> — read coursework assigned to the user (courses.courseWork.list, filtered to PUBLISHED).</item>
/// <item><c>classroom.coursework.me</c> — turn in the user's own coursework and modify its attachments.</item>
/// <item><c>drive.file</c> — upload files created by this app and attach them to a submission.</item>
/// </list>
/// <para>
/// Read scopes and write scopes are deliberately separate. The write scopes are
/// used only by the explicit submission action; syncing coursework stays
/// read-only.
/// </para>
/// <para>
/// <see cref="CurrentVersion"/> is persisted with the token. When the set of
/// requested scopes changes, the stored token must be discarded so Google shows
/// the consent screen again; a token cannot be upgraded silently.
/// </para>
/// </summary>
public static class GoogleOAuthScopes
{
    /// <summary>Bump when <see cref="All"/> changes.</summary>
    public const int CurrentVersion = 2;

    public const string OpenId = "openid";
    public const string UserInfoProfile = "https://www.googleapis.com/auth/userinfo.profile";
    public const string UserInfoEmail = "https://www.googleapis.com/auth/userinfo.email";
    public const string ClassroomCoursesReadonly = "https://www.googleapis.com/auth/classroom.courses.readonly";
    public const string ClassroomCourseworkMeReadonly = "https://www.googleapis.com/auth/classroom.coursework.me.readonly";
    public const string ClassroomCourseworkMe = "https://www.googleapis.com/auth/classroom.coursework.me";
    public const string DriveFile = "https://www.googleapis.com/auth/drive.file";

    public static IReadOnlyList<string> All { get; } =
    [
        OpenId,
        UserInfoProfile,
        UserInfoEmail,
        ClassroomCoursesReadonly,
        ClassroomCourseworkMeReadonly,
        ClassroomCourseworkMe,
        DriveFile,
    ];
}
