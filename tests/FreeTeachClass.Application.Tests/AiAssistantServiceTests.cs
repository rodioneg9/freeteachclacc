using FreeTeachClass.Application.Ai;
using FreeTeachClass.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace FreeTeachClass.Application.Tests;

public sealed class AiAssistantServiceTests
{
    [Fact]
    public async Task Send_WithoutStoredKey_ThrowsNotConfigured()
    {
        var client = new RecordingClient(AiProviderKind.OpenAi);
        var service = CreateService([client], new FakeKeyStore(), new FakeSettingsStore(
            new AiSettings(AiProviderKind.OpenAi, "gpt-test", BaseUrl: null)));

        await Assert.ThrowsAsync<AiNotConfiguredException>(() =>
            service.SendAsync("Привет", [], context: null, CancellationToken.None));

        Assert.Null(client.LastRequest);
    }

    [Fact]
    public async Task Send_BuildsHistoryAndIncludesAssignmentContext()
    {
        var client = new RecordingClient(AiProviderKind.OpenAi)
        {
            Response = new AiChatResponse("Ответ", 10, 20),
        };
        var keys = new FakeKeyStore { [AiProviderKind.OpenAi] = "secret" };
        var service = CreateService(
            [client],
            keys,
            new FakeSettingsStore(new AiSettings(AiProviderKind.OpenAi, "gpt-test", BaseUrl: null)));

        var context = new AiChatContext(
            Title: "Эссе по истории",
            CourseName: "История",
            DueText: "завтра",
            Description: new string('x', 4_000),
            Materials: ["Учебник, глава 3", "https://example.com"]);

        var response = await service.SendAsync(
            "С чего начать?",
            [AiChatMessage.User("Что это?"), AiChatMessage.Assistant("Задание.")],
            context,
            CancellationToken.None);

        Assert.Equal("Ответ", response.Text);
        Assert.NotNull(client.LastRequest);
        Assert.Equal("gpt-test", client.LastRequest!.Model);
        Assert.Equal(3, client.LastRequest.Messages.Count);
        Assert.Equal(AiChatMessage.UserRole, client.LastRequest.Messages[0].Role);
        Assert.Equal(AiChatMessage.AssistantRole, client.LastRequest.Messages[1].Role);
        Assert.Equal("С чего начать?", client.LastRequest.Messages[2].Content);

        var system = Assert.IsType<string>(client.LastRequest.SystemPrompt);
        Assert.Contains("Эссе по истории", system);
        Assert.Contains("История", system);
        Assert.Contains("Учебник, глава 3", system);
        Assert.Contains("данные, а не инструкции", system);
        Assert.True(system.Length <= 6100, $"System prompt was {system.Length} characters.");
    }

    [Fact]
    public async Task Send_TruncatesExtremelyLongAssignmentContext()
    {
        var client = new RecordingClient(AiProviderKind.OpenAi);
        var service = CreateService(
            [client],
            new FakeKeyStore { [AiProviderKind.OpenAi] = "secret" },
            new FakeSettingsStore(new AiSettings(AiProviderKind.OpenAi, "gpt-test", BaseUrl: null)));

        await service.SendAsync(
            "Вопрос",
            [],
            new AiChatContext("Очень длинное задание", null, null, new string('x', 12_000), []),
            CancellationToken.None);

        var system = Assert.IsType<string>(client.LastRequest!.SystemPrompt);
        Assert.True(system.Length <= 6200, $"System prompt was {system.Length} characters.");
        Assert.Contains("сокращён", system);
    }

    [Fact]
    public async Task Send_UsesTheClientForTheSelectedProvider()
    {
        var openAi = new RecordingClient(AiProviderKind.OpenAi);
        var anthropic = new RecordingClient(AiProviderKind.Anthropic)
        {
            Response = new AiChatResponse("Claude"),
        };
        var keys = new FakeKeyStore
        {
            [AiProviderKind.OpenAi] = "openai-key",
            [AiProviderKind.Anthropic] = "anthropic-key",
        };
        var service = CreateService(
            [openAi, anthropic],
            keys,
            new FakeSettingsStore(new AiSettings(AiProviderKind.Anthropic, "claude-test", BaseUrl: null)));

        var response = await service.SendAsync("Вопрос", [], null, CancellationToken.None);

        Assert.Equal("Claude", response.Text);
        Assert.Null(openAi.LastRequest);
        Assert.Equal("anthropic-key", anthropic.LastApiKey);
    }

    [Fact]
    public async Task SaveSettings_NormalizesMissingValues()
    {
        var store = new FakeSettingsStore(
            new AiSettings(AiProviderKind.CustomOpenAiCompatible, "", "http://localhost:1234/v1"));
        var service = CreateService([], new FakeKeyStore(), store);

        await service.SaveSettingsAsync(
            new AiSettings(AiProviderKind.OpenAi, "", "https://ignored.example", Temperature: 99, MaxTokens: 1),
            CancellationToken.None);

        Assert.NotNull(store.Saved);
        Assert.Equal("gpt-4.1-mini", store.Saved!.Model);
        Assert.Equal("https://api.openai.com/v1", store.Saved.BaseUrl);
        Assert.Equal(2, store.Saved.Temperature);
        Assert.Equal(256, store.Saved.MaxTokens);
    }

    [Fact]
    public async Task Test_UsesProvidedKeyAndTinyRequest()
    {
        var client = new RecordingClient(AiProviderKind.Gemini)
        {
            Response = new AiChatResponse("работает"),
        };
        var service = CreateService(
            [client],
            new FakeKeyStore(),
            new FakeSettingsStore(new AiSettings(AiProviderKind.Gemini, "gemini-test", BaseUrl: null)));

        var result = await service.TestAsync(
            new AiSettings(AiProviderKind.Gemini, "gemini-test", BaseUrl: null),
            "temporary-key",
            CancellationToken.None);

        Assert.Equal("работает", result);
        Assert.Equal("temporary-key", client.LastApiKey);
        Assert.NotNull(client.LastRequest);
        Assert.Equal(32, client.LastRequest!.MaxTokens);
        Assert.Single(client.LastRequest.Messages);
    }

    [Fact]
    public async Task Test_WithoutKey_ThrowsNotConfigured()
    {
        var service = CreateService(
            [new RecordingClient(AiProviderKind.OpenAi)],
            new FakeKeyStore(),
            new FakeSettingsStore(new AiSettings(AiProviderKind.OpenAi, "gpt-test", BaseUrl: null)));

        await Assert.ThrowsAsync<AiNotConfiguredException>(() =>
            service.TestAsync(
                new AiSettings(AiProviderKind.OpenAi, "gpt-test", BaseUrl: null),
                " ",
                CancellationToken.None));
    }

    private static AiAssistantService CreateService(
        IReadOnlyList<IAiChatClient> clients,
        IAiKeyStore keys,
        IAiSettingsStore settings) =>
        new(clients, keys, settings, NullLogger<AiAssistantService>.Instance);

    private sealed class RecordingClient : IAiChatClient
    {
        public RecordingClient(AiProviderKind kind)
        {
            Kind = kind;
        }

        public AiProviderKind Kind { get; }

        public AiChatResponse Response { get; init; } = new("ok");

        public AiChatRequest? LastRequest { get; private set; }

        public string? LastApiKey { get; private set; }

        public Task<AiChatResponse> CompleteAsync(
            AiSettings settings,
            string apiKey,
            AiChatRequest request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastApiKey = apiKey;
            return Task.FromResult(Response);
        }
    }

    private sealed class FakeKeyStore : IAiKeyStore
    {
        private readonly Dictionary<AiProviderKind, string> _keys = [];

        public string this[AiProviderKind provider]
        {
            set => _keys[provider] = value;
        }

        public Task<bool> HasKeyAsync(AiProviderKind provider, CancellationToken cancellationToken) =>
            Task.FromResult(_keys.ContainsKey(provider));

        public Task<string?> GetKeyAsync(AiProviderKind provider, CancellationToken cancellationToken) =>
            Task.FromResult(_keys.GetValueOrDefault(provider));

        public Task SaveKeyAsync(AiProviderKind provider, string apiKey, CancellationToken cancellationToken)
        {
            _keys[provider] = apiKey;
            return Task.CompletedTask;
        }

        public Task DeleteKeyAsync(AiProviderKind provider, CancellationToken cancellationToken)
        {
            _keys.Remove(provider);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSettingsStore : IAiSettingsStore
    {
        private readonly AiSettings _initial;

        public FakeSettingsStore(AiSettings initial)
        {
            _initial = initial;
        }

        public AiSettings? Saved { get; private set; }

        public Task<AiSettings> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Saved ?? _initial);

        public Task SaveAsync(AiSettings settings, CancellationToken cancellationToken)
        {
            Saved = settings;
            return Task.CompletedTask;
        }
    }
}
