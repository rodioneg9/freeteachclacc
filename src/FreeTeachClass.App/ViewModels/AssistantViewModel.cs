using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FreeTeachClass.Application.Ai;
using Microsoft.Extensions.Logging;

namespace FreeTeachClass.App.ViewModels;

/// <summary>One bubble in the assistant chat.</summary>
public sealed class AiMessageViewModel
{
    public required string Author { get; init; }

    public required string Text { get; init; }

    public required bool IsUser { get; init; }

    public string TimeText { get; init; } = DateTimeOffset.Now.ToString("HH:mm");
}

/// <summary>
/// AI chat page. Keeps the conversation in memory (deliberately not persisted)
/// and optionally attaches the assignment that was open in the detail page.
/// </summary>
public sealed partial class AssistantViewModel : ObservableObject
{
    private const string ExplainPrompt =
        "Объясни это задание простыми словами и скажи, с чего начать.";

    private const string PlanPrompt =
        "Составь пошаговый план выполнения этого задания с примерными сроками.";

    private const string CheckPrompt =
        "Проверь мой подход к заданию и укажи, что может пойти не так.";

    private readonly IAiAssistantService _ai;
    private readonly INavigator _navigator;
    private readonly ILogger<AssistantViewModel> _logger;
    private readonly List<AiChatMessage> _history = [];

    private AiChatContext? _context;

    [ObservableProperty] public partial string Input { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial string? StatusMessage { get; set; }
    [ObservableProperty] public partial bool HasKey { get; set; }
    [ObservableProperty] public partial string ProviderText { get; set; } = "AI не настроен";
    [ObservableProperty] public partial string? ContextTitle { get; set; }
    [ObservableProperty] public partial bool HasContext { get; set; }

    public ObservableCollection<AiMessageViewModel> Messages { get; } = [];

    public bool IsEmpty => Messages.Count == 0;

    public bool ShowEmptyState => Messages.Count == 0 && !IsBusy;

    public bool ShowSetupHint => !HasKey;

    public bool CanSend => HasKey && !IsBusy && !string.IsNullOrWhiteSpace(Input);

    public AssistantViewModel(
        IAiAssistantService ai,
        INavigator navigator,
        ILogger<AssistantViewModel> logger)
    {
        _ai = ai;
        _navigator = navigator;
        _logger = logger;
        Messages.CollectionChanged += OnMessagesChanged;
    }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var settings = await _ai.LoadSettingsAsync(cancellationToken).ConfigureAwait(true);
            var info = AiProviderCatalog.Get(settings.Provider);
            ProviderText = $"{info.Name} · {settings.Model}";
            HasKey = await _ai.HasKeyAsync(settings.Provider, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load AI settings");
            ProviderText = "AI не настроен";
            HasKey = false;
        }
    }

    /// <summary>Attaches the currently opened assignment to the next questions.</summary>
    public void SetContext(AssignmentItemViewModel item)
    {
        _context = new AiChatContext(
            item.Title,
            item.CourseName,
            item.DueText,
            item.Description,
            item.Materials.Select(material => material.Title).ToArray());

        ContextTitle = item.Title;
        HasContext = true;
    }

    partial void OnInputChanged(string value) => OnPropertyChanged(nameof(CanSend));

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSend));
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    partial void OnHasKeyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSend));
        OnPropertyChanged(nameof(ShowSetupHint));
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task SendAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        if (!HasKey)
        {
            StatusMessage = "Сначала добавьте API-ключ в настройках AI.";
            return;
        }

        var text = Input.Trim();
        if (text.Length == 0)
        {
            return;
        }

        var history = _history.ToArray();
        Input = string.Empty;
        StatusMessage = null;
        IsBusy = true;

        var userMessage = new AiMessageViewModel
        {
            Author = "Вы",
            Text = text,
            IsUser = true,
        };
        Messages.Add(userMessage);
        _history.Add(AiChatMessage.User(text));

        try
        {
            var response = await _ai
                .SendAsync(text, history, _context, cancellationToken)
                .ConfigureAwait(true);

            var answer = response.Text.Trim();
            Messages.Add(new AiMessageViewModel
            {
                Author = "AI-помощник",
                Text = answer,
                IsUser = false,
            });
            _history.Add(AiChatMessage.Assistant(answer));
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Запрос отменён.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI request failed");
            StatusMessage = ErrorMessages.For(ex);
            if (ex is AiNotConfiguredException)
            {
                HasKey = false;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task AskExplainAsync() => QuickAskAsync(ExplainPrompt);

    [RelayCommand]
    private Task AskPlanAsync() => QuickAskAsync(PlanPrompt);

    [RelayCommand]
    private Task AskCheckAsync() => QuickAskAsync(CheckPrompt);

    private async Task QuickAskAsync(string prompt)
    {
        if (!HasKey)
        {
            StatusMessage = "Сначала добавьте API-ключ в настройках AI.";
            return;
        }

        Input = prompt;
        await SendCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void ClearChat()
    {
        Messages.Clear();
        _history.Clear();
        StatusMessage = null;
    }

    [RelayCommand]
    private void ClearContext()
    {
        _context = null;
        ContextTitle = null;
        HasContext = false;
    }

    [RelayCommand]
    private void OpenSettings() => _navigator.Navigate(PageKey.Settings);

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(ShowEmptyState));
    }
}
