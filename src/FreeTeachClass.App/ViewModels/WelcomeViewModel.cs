using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FreeTeachClass.Application.Services;
using FreeTeachClass.Infrastructure.Google;
using Google.Apis.Auth.OAuth2.Responses;
using Microsoft.Extensions.Logging;

namespace FreeTeachClass.App.ViewModels;

/// <summary>
/// Welcome screen: connect Google Classroom via OAuth 2.0.
/// </summary>
public sealed partial class WelcomeViewModel : ObservableObject
{
    private readonly IAuthenticationService _authentication;
    private readonly IClassroomSyncService _sync;
    private readonly INavigator _navigator;
    private readonly ILogger<WelcomeViewModel> _logger;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string? HintMessage { get; set; }

    public WelcomeViewModel(
        IAuthenticationService authentication,
        IClassroomSyncService sync,
        GoogleOAuthOptions oauthOptions,
        INavigator navigator,
        ILogger<WelcomeViewModel> logger)
    {
        _authentication = authentication;
        _sync = sync;
        _navigator = navigator;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(oauthOptions.ClientId))
        {
            HintMessage =
                "Приложение не настроено: отсутствуют учётные данные Google OAuth. " +
                "Как настроить — см. README.md (переменная окружения " +
                $"{GoogleOAuthOptions.ClientIdEnvironmentVariable}, файл oauth-client.json " +
                "или Client ID/Client Secret в GoogleOAuthDefaults).";
        }
    }

    private bool CanConnect => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _authentication.SignInAsync(cancellationToken).ConfigureAwait(true);

            try
            {
                await _sync.SyncAsync(cancellationToken).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Auth succeeded; the dashboard will show the sync problem and offer retry.
                _logger.LogWarning(ex, "Initial sync right after sign-in failed");
            }

            _navigator.Navigate(PageKey.Dashboard);
        }
        catch (OperationCanceledException)
        {
            // User closed the browser / cancelled — not an error state.
        }
        catch (MissingOAuthConfigurationException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (TokenResponseException)
        {
            ErrorMessage = "Не удалось завершить авторизацию. Попробуйте ещё раз.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sign-in failed");
            ErrorMessage = ErrorMessages.For(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
