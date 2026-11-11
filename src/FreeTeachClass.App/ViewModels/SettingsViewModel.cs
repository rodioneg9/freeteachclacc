using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FreeTeachClass.Application.Ai;
using FreeTeachClass.Application.Services;
using FreeTeachClass.Domain.Abstractions;
using FreeTeachClass.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace FreeTeachClass.App.ViewModels;

/// <summary>
/// Settings: theme, account info, data refresh and sign-out.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IAuthenticationService _authentication;
    private readonly IClassroomSyncService _sync;
    private readonly ILocalDataStore _store;
    private readonly IAiAssistantService _ai;
    private readonly ThemeService _themeService;
    private readonly INavigator _navigator;
    private readonly ILogger<SettingsViewModel> _logger;

    private bool _suppressAiSettingsChanged;

    [ObservableProperty] public partial int ThemeIndex { get; set; }
    [ObservableProperty] public partial string UserName { get; set; }
    [ObservableProperty] public partial string UserEmail { get; set; }
    [ObservableProperty] public partial string LastSyncText { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial string? StatusMessage { get; set; }

    [ObservableProperty] public partial AiProviderInfo? SelectedAiProvider { get; set; }
    [ObservableProperty] public partial string AiModel { get; set; } = string.Empty;
    [ObservableProperty] public partial string AiBaseUrl { get; set; } = string.Empty;
    [ObservableProperty] public partial string AiKeyInput { get; set; } = string.Empty;
    [ObservableProperty] public partial string AiKeyStatus { get; set; } = "Ключ не задан.";
    [ObservableProperty] public partial string? AiStatusMessage { get; set; }
    [ObservableProperty] public partial bool IsAiBusy { get; set; }

    public IReadOnlyList<AiProviderInfo> AiProviders => AiProviderCatalog.All;

    public ObservableCollection<string> AiModels { get; } = [];

    public bool IsCustomProvider => SelectedAiProvider?.Kind == AiProviderKind.CustomOpenAiCompatible;

    public string AiKeyHint => SelectedAiProvider?.KeyHint ?? string.Empty;

    public Uri? AiKeyUri =>
        Uri.TryCreate(SelectedAiProvider?.KeyUrl, UriKind.Absolute, out var uri) ? uri : null;

    public bool HasAiKeyUri => AiKeyUri is not null;

    public SettingsViewModel(
        IAuthenticationService authentication,
        IClassroomSyncService sync,
        ILocalDataStore store,
        IAiAssistantService ai,
        ThemeService themeService,
        INavigator navigator,
        ILogger<SettingsViewModel> logger)
    {
        _authentication = authentication;
        _sync = sync;
        _store = store;
        _ai = ai;
        _themeService = themeService;
        _navigator = navigator;
        _logger = logger;

        UserName = "—";
        UserEmail = "—";
        LastSyncText = "ещё не синхронизировано";

        ThemeIndex = _themeService.Current switch
        {
            AppTheme.Light => 1,
            AppTheme.Dark => 2,
            _ => 0,
        };
    }

    partial void OnThemeIndexChanged(int value)
    {
        _themeService.Current = value switch
        {
            1 => AppTheme.Light,
            2 => AppTheme.Dark,
            _ => AppTheme.System,
        };
        _navigator.RefreshTheme();
    }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var user = await _store.LoadUserAsync(cancellationToken).ConfigureAwait(true);
        UserName = string.IsNullOrWhiteSpace(user?.Name) ? "—" : user!.Name;
        UserEmail = string.IsNullOrWhiteSpace(user?.Email) ? "—" : user!.Email;

        var lastSync = await _store.LoadLastSyncUtcAsync(cancellationToken).ConfigureAwait(true);
        LastSyncText = lastSync is null
            ? "ещё не синхронизировано"
            : lastSync.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm");

        await LoadAiAsync(cancellationToken).ConfigureAwait(true);
    }

    partial void OnSelectedAiProviderChanged(AiProviderInfo? value)
    {
        if (value is null)
        {
            return;
        }

        RefreshModelList(value);
        NotifyAiProviderDependent();

        if (_suppressAiSettingsChanged)
        {
            return;
        }

        AiModel = value.DefaultModel;
        AiBaseUrl = value.DefaultBaseUrl ?? string.Empty;
        AiStatusMessage = null;
        _ = RefreshKeyStatusAsync(CancellationToken.None);
    }

    private async Task LoadAiAsync(CancellationToken cancellationToken)
    {
        try
        {
            var settings = await _ai.LoadSettingsAsync(cancellationToken).ConfigureAwait(true);

            _suppressAiSettingsChanged = true;
            try
            {
                SelectedAiProvider = AiProviders.FirstOrDefault(provider => provider.Kind == settings.Provider)
                                     ?? AiProviders[0];
                RefreshModelList(SelectedAiProvider);
                AiModel = settings.Model;
                AiBaseUrl = settings.BaseUrl ?? string.Empty;
                EnsureCurrentModelInList();
                NotifyAiProviderDependent();
            }
            finally
            {
                _suppressAiSettingsChanged = false;
            }

            await RefreshKeyStatusAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load AI settings");
            AiStatusMessage = ErrorMessages.For(ex);
        }
    }

    private void RefreshModelList(AiProviderInfo? provider)
    {
        AiModels.Clear();
        if (provider is null)
        {
            return;
        }

        foreach (var model in provider.Models)
        {
            AiModels.Add(model);
        }
    }

    private void EnsureCurrentModelInList()
    {
        if (!string.IsNullOrWhiteSpace(AiModel) && !AiModels.Contains(AiModel))
        {
            AiModels.Insert(0, AiModel);
        }
    }

    private void NotifyAiProviderDependent()
    {
        OnPropertyChanged(nameof(IsCustomProvider));
        OnPropertyChanged(nameof(AiKeyHint));
        OnPropertyChanged(nameof(AiKeyUri));
        OnPropertyChanged(nameof(HasAiKeyUri));
    }

    private async Task RefreshKeyStatusAsync(CancellationToken cancellationToken)
    {
        if (SelectedAiProvider is null)
        {
            AiKeyStatus = "Ключ не задан.";
            return;
        }

        try
        {
            var hasKey = await _ai
                .HasKeyAsync(SelectedAiProvider.Kind, cancellationToken)
                .ConfigureAwait(true);
            AiKeyStatus = hasKey
                ? "Ключ сохранён и зашифрован Windows DPAPI."
                : "Ключ не задан.";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read AI key status");
            AiKeyStatus = "Не удалось проверить наличие ключа.";
        }
    }

    private AiSettings BuildAiSettings() =>
        new(
            SelectedAiProvider?.Kind ?? AiProviderKind.OpenAi,
            AiModel,
            IsCustomProvider ? AiBaseUrl : null);

    [RelayCommand]
    private async Task SaveAiSettingsAsync()
    {
        if (IsAiBusy)
        {
            return;
        }

        IsAiBusy = true;
        AiStatusMessage = null;
        try
        {
            await _ai.SaveSettingsAsync(BuildAiSettings(), CancellationToken.None).ConfigureAwait(true);
            EnsureCurrentModelInList();
            AiStatusMessage = "Настройки AI сохранены.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not save AI settings");
            AiStatusMessage = ErrorMessages.For(ex);
        }
        finally
        {
            IsAiBusy = false;
        }
    }

    /// <summary>Called by the settings page after the PasswordBox has filled <see cref="AiKeyInput"/>.</summary>
    public async Task SaveAiKeyAsync()
    {
        if (IsAiBusy || SelectedAiProvider is null)
        {
            return;
        }

        var key = AiKeyInput.Trim();
        if (key.Length == 0)
        {
            AiKeyStatus = "Введите API-ключ.";
            return;
        }

        IsAiBusy = true;
        AiStatusMessage = null;
        try
        {
            await _ai.SaveKeyAsync(SelectedAiProvider.Kind, key, CancellationToken.None).ConfigureAwait(true);
            AiKeyInput = string.Empty;
            await RefreshKeyStatusAsync(CancellationToken.None).ConfigureAwait(true);
            AiStatusMessage = "API-ключ сохранён.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not save AI key");
            AiStatusMessage = ErrorMessages.For(ex);
        }
        finally
        {
            IsAiBusy = false;
        }
    }

    /// <summary>Called by the settings page; deletes the stored key for the selected provider.</summary>
    public async Task DeleteAiKeyAsync()
    {
        if (IsAiBusy || SelectedAiProvider is null)
        {
            return;
        }

        IsAiBusy = true;
        AiStatusMessage = null;
        try
        {
            await _ai.DeleteKeyAsync(SelectedAiProvider.Kind, CancellationToken.None).ConfigureAwait(true);
            AiKeyInput = string.Empty;
            await RefreshKeyStatusAsync(CancellationToken.None).ConfigureAwait(true);
            AiStatusMessage = "API-ключ удалён.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete AI key");
            AiStatusMessage = ErrorMessages.For(ex);
        }
        finally
        {
            IsAiBusy = false;
        }
    }

    /// <summary>Tests the typed key, or the stored one when the field is empty.</summary>
    public async Task TestAiKeyAsync()
    {
        if (IsAiBusy || SelectedAiProvider is null)
        {
            return;
        }

        IsAiBusy = true;
        AiStatusMessage = "Проверяем подключение…";
        try
        {
            var key = string.IsNullOrWhiteSpace(AiKeyInput) ? null : AiKeyInput.Trim();
            var answer = await _ai
                .TestAsync(BuildAiSettings(), key, CancellationToken.None)
                .ConfigureAwait(true);

            var preview = answer.Length > 120 ? answer[..120] + "…" : answer;
            AiStatusMessage = $"Проверка успешна. Ответ модели: {preview}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI key test failed");
            AiStatusMessage = ErrorMessages.For(ex);
        }
        finally
        {
            IsAiBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshDataAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            var result = await _sync.SyncAsync(cancellationToken).ConfigureAwait(true);
            await LoadAsync(cancellationToken).ConfigureAwait(true);
            StatusMessage = $"Данные обновлены: {result.CoursesSynced} курсов, " +
                            $"{result.AssignmentsSynced} заданий.";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Manual refresh failed");
            StatusMessage = ErrorMessages.For(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SignOutAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _authentication.SignOutAsync(cancellationToken).ConfigureAwait(true);
            _navigator.Navigate(PageKey.Welcome);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sign-out failed");
            StatusMessage = ErrorMessages.For(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
