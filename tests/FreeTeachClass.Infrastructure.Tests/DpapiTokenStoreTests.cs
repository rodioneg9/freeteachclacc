using FreeTeachClass.Infrastructure.Google;
using FreeTeachClass.Infrastructure.Storage;
using Google.Apis.Auth.OAuth2.Responses;

namespace FreeTeachClass.Infrastructure.Tests;

/// <summary>
/// DPAPI token storage: tokens must survive a store→read cycle and must never
/// appear as plain text on disk. Windows-only (DPAPI), so every test guards
/// itself on the current platform.
/// </summary>
public sealed class DpapiTokenStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "FreeTeachClass.Tests",
        Guid.NewGuid().ToString("N"));

    private string TokenFilePath => Path.Combine(_directory, "tokens.dat");

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // A leftover temp directory must not fail the test run.
        }
    }

    [Fact]
    public async Task TokenWrittenByTheFlow_IsVisibleToHasTokenAndNeverPlainTextOnDisk()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var store = new DpapiTokenStore(TokenFilePath);

        Assert.False(await store.HasTokenAsync(CancellationToken.None));

        // Same key Google's authorization flow writes the TokenResponse under.
        await store.StoreAsync(GoogleAuthGateway.FlowUser, new TokenResponse
        {
            RefreshToken = "refresh-secret-value",
            AccessToken = "access-secret-value",
        });

        Assert.True(await store.HasTokenAsync(CancellationToken.None));

        var token = await store.GetAsync<TokenResponse>(GoogleAuthGateway.FlowUser);
        Assert.NotNull(token);
        Assert.Equal("refresh-secret-value", token!.RefreshToken);

        // The file exists, but none of the secrets are readable in it.
        Assert.True(File.Exists(TokenFilePath));
        var raw = await File.ReadAllTextAsync(TokenFilePath, CancellationToken.None);
        Assert.DoesNotContain("refresh-secret-value", raw);
        Assert.DoesNotContain("access-secret-value", raw);
    }

    [Fact]
    public async Task Clear_RemovesTheStoredSession()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var store = new DpapiTokenStore(TokenFilePath);
        await store.StoreAsync(GoogleAuthGateway.FlowUser, new TokenResponse
        {
            RefreshToken = "refresh-secret-value",
        });
        Assert.True(await store.HasTokenAsync(CancellationToken.None));

        await store.ClearAsync(CancellationToken.None);

        Assert.False(await store.HasTokenAsync(CancellationToken.None));
        Assert.Null(await store.GetAsync<TokenResponse>(GoogleAuthGateway.FlowUser));
    }

    [Fact]
    public async Task UnknownKey_ReadsAsNoToken()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var store = new DpapiTokenStore(TokenFilePath);

        Assert.False(await store.HasTokenAsync(CancellationToken.None));
        Assert.Null(await store.GetAsync<TokenResponse>("some-other-user"));
    }
}
