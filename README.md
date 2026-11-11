# FreeTeachClass

A native Windows application—a "smart" Google Classroom client for students and teachers.
It retrieves courses and assignments via the official Google Classroom API, caches them locally,
and presents them through a user-friendly interface (built with WinUI 3). This is the initial MVP,
featuring OAuth login, synchronization, a dashboard, lists of subjects and assignments,
and a detailed view for individual assignments (including descriptions and attachments).
AI-powered features, XP, and achievement systems have been intentionally omitted at this stage.

This is neither a web app nor an Electron-based application; it is a standard .NET/WinUI 3 process
that runs as a native Windows program (self-contained—no separate .NET installation required).

## Architecture

The architecture consists of four layers, with dependencies always pointing inward.
The UI layer never interacts with the Google API directly:

```
src/
├── FreeTeachClass.Domain          # Models (User, Course, Assignment, CourseMaterial)
│                                  # and interfaces: IClassroomDataSource, ILocalDataStore,
│                                  # ISecureTokenStore. Contains no external dependencies.
├── FreeTeachClass.Application     # Services and use cases: ClassroomSyncService,
│                                  # DashboardService, AuthenticationService.
│                                  # Defines contracts such as IGoogleAuthGateway.
├── FreeTeachClass.Infrastructure  # Implementations: GoogleAuthGateway (OAuth 2.0 +
│                                  # PKCE), GoogleClassroomDataSource (Classroom API),
│                                  # GoogleClassroomMapper, JsonLocalDataStore,
│                                  # DpapiTokenStore (utilizing Windows DPAPI).
└── FreeTeachClass.App             # WinUI 3 / MVVM: pages, ViewModels, dependency injection (DI),
# navigation, theming, logging, and crash reporting.
tests/
├── FreeTeachClass.Application.Tests   # Unit tests for synchronization and services
└── FreeTeachClass.Infrastructure.Tests # API mapping, JSON storage, DPAPI tokens

Synchronization data flow:

```
Google Classroom API → IClassroomDataSource → ClassroomSyncService (Application)
→ ILocalDataStore (JSON cache in %LOCALAPPDATA%) → ViewModel → UI
```

Screens read from the cache rather than the API; Google is only called during manual or background synchronization. This ensures that opening a page is not "slow" and avoids hitting API quota limits. All external dependencies are injected via interfaces, making their implementations interchangeable.

## Stack

| Component | Choice |
|---|---|
| Runtime | .NET 8 (self-contained application, including Windows App SDK) |
| UI | WinUI 3 + Windows App SDK 2.5.1, MVVM |
| MVVM | CommunityToolkit.Mvvm 8.4 |
| DI / Logging | Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Logging |
| Google API | `Google.Apis.Classroom.v1` (official client) |
| OAuth 2.0 | System browser + loopback redirect + PKCE (for installed apps) |
| Tests | xunit (41 tests) |
| Build | .NET SDK 10 (required for building), `FreeTeachClass.slnx` solution |

## Build and Run

Requirements: Windows 10 (1809+), .NET SDK 10.x (required for building; not needed for running the application). ```powershell
# Build the entire solution
dotnet build FreeTeachClass.slnx -c Debug -p:Platform=x64

# Run (verified build output path)
.\src\FreeTeachClass.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\FreeTeachClass.App.exe
```

> Note: If you build the project directly
> (`dotnet build src\FreeTeachClass.App\FreeTeachClass.App.csproj -c Debug -p:Platform=x64`),
> the output will be located in `bin\x64\Debug\...`. Both directories contain a valid build; you can verify that the
> .exe file is up-to-date by checking its timestamp (if in doubt, simply rebuild the solution).

Tests:

```powershell
dotnet test FreeTeachClass.slnx -c Debug -p:Platform=x64
```

Expected result: `Failed: 0` for both test projects (12 + 29 = 41 tests in total).

## Google Cloud and OAuth Setup

The application can be launched without OAuth credentials; it will display a login screen with a prompt.
Google login becomes available after a one-time setup process.

### 1. Google Cloud Project

1. Open <https://console.cloud.google.com/> and create a new project.
2. Go to **APIs & Services → Library** and enable the **Google Classroom API**. 
(The Google People API is not required; the email address and name are retrieved via OpenID Connect userinfo.)

### 2. OAuth Consent Screen

1. Go to **APIs & Services → OAuth consent screen**:
Select the **External** user type, then provide an application name and a support email address. 2. **Test users** → add the Google accounts you intend to use
(while the application is in "Testing" status, other accounts will not be able to sign in).
3. **Scopes → Add or remove scopes** — add exactly these (the minimum set,
read-only; verified against official Google documentation, as of October 2026):

| Scope | Purpose | 
|---|---| 
| `openid` | Identification (OpenID Connect) | 
| `https://www.googleapis.com/auth/userinfo.profile` | User's name | 
| `https://www.googleapis.com/auth/userinfo.email` | User's email | 
| `https://www.googleapis.com/auth/classroom.courses.readonly` | List of courses | 
| `https://www.googleapis.com/auth/classroom.coursework.me.readonly` | Assignments assigned to the user | 

No "write" scopes are used — the application does not modify anything in Classroom. 

### 3. OAuth Client

1. **APIs & Services → Credentials → Create credentials → OAuth client ID**.
2. Application type: **Desktop app**.
3. Copy the Client ID and Client secret.

Official documentation:
[OAuth 2.0 for installed apps](https://developers.google.com/identity/protocols/oauth2/native-app),
[Google Classroom API](https://developers.google.com/classroom/reference/rest).

## Providing OAuth Credentials to the Application

There are three methods (in order of precedence: environment variables → file → source code). The first non-empty option determines the application's configuration.

**Option A — Environment Variables:**

```powershell
$env:FREETEACHCLASS_GOOGLE_CLIENT_ID = "<client-id>.apps.googleusercontent.com"
$env:FREETEACHCLASS_GOOGLE_CLIENT_SECRET = "<client-secret>"
```

**Option B — File:** `%LOCALAPPDATA%\FreeTeachClass\oauth-client.json`
(uses the standard Google client format; a "flat" structure—`{"clientId": "…", "clientSecret": "…"}`—is also accepted):

```json
{
"installed": {
"client_id": "<client-id>.apps.googleusercontent.com",
"client_secret": "<client-secret>"
}
}
```

This file (and the entire `%LOCALAPPDATA%\FreeTeachClass\` directory) is excluded from Git via `.gitignore`.

**Option C — Directly in the source code** (for custom builds that do not rely on external files or environment variables). Open the file
`src/FreeTeachClass.Infrastructure/Google/GoogleOAuthDefaults.cs` and enter the
values ​​from the Google Cloud Console:

```csharp
public static class GoogleOAuthDefaults
{
public static string ClientId { get; set; } = "<client-id>.apps.googleusercontent.com"; 
public static string? ClientSecret { get; set; } = "<client-id>";
}
```

After making these changes, rebuild the application; the sign-in feature will work immediately. The settings screen and the application design remain unchanged.

> Security note: For a desktop client, Google considers the "client secret" to be non-sensitive (as a "public client" cannot securely store secrets), and request signing is handled by Google's servers regardless. While embedding these values ​​in the code is convenient for personal use, options A and B are preferable if you intend to distribute the application publicly.

Empty or placeholder values ​​(such as `<client-id>`, `PASTE_ME`, `YOUR_…`, etc.)
are ignored; in such cases, the application will launch as usual and display a prompt on the sign-in screen explaining how to configure OAuth.

## What happens during sign-in

- The system browser opens (Google credentials are entered exclusively on Google's website—the application never requests or stores your password).
- The flow redirects to a local loopback address (`http://127.0.0.1:<port>/`), using the OAuth 2.0 authorization flow for installed applications with PKCE.
- Tokens are stored only within secure Windows storage (DPAPI, scoped to the current user); tokens and personal data are never written to logs in plaintext.

## Local Data

Directory: `%LOCALAPPDATA%\FreeTeachClass\`

| File/Folder | Purpose |
|---|---|
| `cache\` | JSON cache of courses/assignments (populated via synchronization) |
| `tokens.dat` | OAuth tokens, encrypted using DPAPI |
| `ui-settings.json` | UI settings (theme, etc.) |
| `crash.log` | Last-resort log: unhandled exceptions (excludes tokens and PII) |
| `app-YYYYMMDD.log` | Standard operational logs (Information level and above; excludes sensitive data) |
| `oauth-client.json` | OAuth client configuration (created by the user; not included in the repository) |

## Screen Structure (MVP)

- **Welcome** — sign-in via Google or instructions for setting up OAuth.
- **Dashboard** — synchronization status, upcoming assignments, and a summary of courses.
- **Courses** — list of subjects/courses.
- **Assignments** — list of assignments, filterable by subject.
- **Assignment Detail** — full assignment description and attachments. Links within the title, description, and attachments are clickable and open in the browser.
- **Settings** — theme selection, re-synchronization, and sign-out.
- **Plan / AI Assistant / Achievements** — placeholders (reserved for future versions).

## MVP Limitations

- Read-only access to Google Classroom data (no creation or modification of assignments).
- AI features, XP, and achievements are not yet implemented (planned for future phases).
- Synchronizes courses and published assignments; drafts are not displayed.
