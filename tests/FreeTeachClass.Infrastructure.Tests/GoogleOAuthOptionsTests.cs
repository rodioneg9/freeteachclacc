using FreeTeachClass.Infrastructure.Google;

namespace FreeTeachClass.Infrastructure.Tests;

/// <summary>
/// Resolution order of the OAuth client credentials: environment variables →
/// local config file → values hardcoded in <see cref="GoogleOAuthDefaults"/>.
/// The real user profile and the real environment are never touched.
/// </summary>
public sealed class GoogleOAuthOptionsTests : IDisposable
{
    private readonly string _appDataRoot = Path.Combine(
        Path.GetTempPath(),
        "FreeTeachClass.Tests",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        GoogleOAuthDefaults.Reset();
        try
        {
            if (Directory.Exists(_appDataRoot))
            {
                Directory.Delete(_appDataRoot, recursive: true);
            }
        }
        catch (IOException)
        {
            // A leftover temp directory must not fail the test run.
        }
    }

    [Fact]
    public void NothingConfigured_MeansNotConfigured()
    {
        var loaded = GoogleOAuthOptions.TryLoad(out var options, _appDataRoot, _ => null);

        Assert.False(loaded);
        Assert.Null(options);
    }

    [Fact]
    public void BuiltInDefaults_AreUsedWhenNothingElseIsSet()
    {
        GoogleOAuthDefaults.ClientId = "1234567890-abcdef.apps.googleusercontent.com";
        GoogleOAuthDefaults.ClientSecret = "GOCSPX-secret";

        var loaded = GoogleOAuthOptions.TryLoad(out var options, _appDataRoot, _ => null);

        Assert.True(loaded);
        Assert.NotNull(options);
        Assert.Equal("1234567890-abcdef.apps.googleusercontent.com", options!.ClientId);
        Assert.Equal("GOCSPX-secret", options.ClientSecret);
    }

    [Fact]
    public void EnvironmentVariables_WinOverBuiltInDefaults()
    {
        GoogleOAuthDefaults.ClientId = "built-in.apps.googleusercontent.com";
        GoogleOAuthDefaults.ClientSecret = "built-in-secret";

        var loaded = GoogleOAuthOptions.TryLoad(
            out var options,
            _appDataRoot,
            name => name switch
            {
                GoogleOAuthOptions.ClientIdEnvironmentVariable => "from-env.apps.googleusercontent.com",
                GoogleOAuthOptions.ClientSecretEnvironmentVariable => "env-secret",
                _ => null,
            });

        Assert.True(loaded);
        Assert.Equal("from-env.apps.googleusercontent.com", options!.ClientId);
        Assert.Equal("env-secret", options.ClientSecret);
    }

    [Fact]
    public void ConfigFile_WinsOverBuiltInDefaults()
    {
        GoogleOAuthDefaults.ClientId = "built-in.apps.googleusercontent.com";
        Directory.CreateDirectory(_appDataRoot);
        File.WriteAllText(
            Path.Combine(_appDataRoot, "oauth-client.json"),
            """{"installed":{"client_id":"from-file.apps.googleusercontent.com","client_secret":"file-secret"}}""");

        var loaded = GoogleOAuthOptions.TryLoad(out var options, _appDataRoot, _ => null);

        Assert.True(loaded);
        Assert.Equal("from-file.apps.googleusercontent.com", options!.ClientId);
        Assert.Equal("file-secret", options.ClientSecret);
    }

    [Fact]
    public void ConfigFile_AcceptsFlatShape()
    {
        Directory.CreateDirectory(_appDataRoot);
        File.WriteAllText(
            Path.Combine(_appDataRoot, "oauth-client.json"),
            """{"clientId":"flat.apps.googleusercontent.com","clientSecret":"flat-secret"}""");

        var loaded = GoogleOAuthOptions.TryLoad(out var options, _appDataRoot, _ => null);

        Assert.True(loaded);
        Assert.Equal("flat.apps.googleusercontent.com", options!.ClientId);
        Assert.Equal("flat-secret", options.ClientSecret);
    }

    [Fact]
    public void BrokenConfigFile_FallsBackToBuiltInDefaults()
    {
        GoogleOAuthDefaults.ClientId = "built-in.apps.googleusercontent.com";
        Directory.CreateDirectory(_appDataRoot);
        File.WriteAllText(Path.Combine(_appDataRoot, "oauth-client.json"), "{ not json");

        var loaded = GoogleOAuthOptions.TryLoad(out var options, _appDataRoot, _ => null);

        Assert.True(loaded);
        Assert.Equal("built-in.apps.googleusercontent.com", options!.ClientId);
    }

    [Theory]
    [InlineData("<client-id>.apps.googleusercontent.com")]
    [InlineData("PASTE_ME")]
    [InlineData("YOUR_CLIENT_ID")]
    [InlineData("   ")]
    public void PlaceholderValues_AreIgnored(string placeholder)
    {
        GoogleOAuthDefaults.ClientId = placeholder;

        var loaded = GoogleOAuthOptions.TryLoad(out var options, _appDataRoot, _ => null);

        Assert.False(loaded);
        Assert.Null(options);
    }

    [Fact]
    public void QuotedValue_IsUnwrapped()
    {
        GoogleOAuthDefaults.ClientId = "\"quoted.apps.googleusercontent.com\"";

        var loaded = GoogleOAuthOptions.TryLoad(out var options, _appDataRoot, _ => null);

        Assert.True(loaded);
        Assert.Equal("quoted.apps.googleusercontent.com", options!.ClientId);
    }
}
