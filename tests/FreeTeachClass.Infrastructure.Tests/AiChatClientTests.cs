using System.Net;
using System.Text;
using FreeTeachClass.Application.Ai;
using FreeTeachClass.Domain.Enums;
using FreeTeachClass.Infrastructure.Ai;

namespace FreeTeachClass.Infrastructure.Tests;

/// <summary>
/// Provider adapters are tested through their HTTP boundary: a fake handler
/// captures the outgoing request and returns a canned provider response.
/// </summary>
public sealed class AiChatClientTests
{
    [Fact]
    public async Task OpenAi_SendsBearerTokenAndParsesTextAndUsage()
    {
        var handler = new FakeHandler(
            HttpStatusCode.OK,
            """
            {
              "choices": [ { "message": { "content": "  Привет!  " } } ],
              "usage": { "prompt_tokens": 12, "completion_tokens": 7 }
            }
            """);

        using var client = new OpenAiCompatibleChatClient(AiProviderKind.OpenAi, handler);
        var settings = new AiSettings(AiProviderKind.OpenAi, "gpt-test", BaseUrl: "https://api.example.test/v1");

        var response = await client.CompleteAsync(
            settings,
            "sk-test-key",
            new AiChatRequest(
                "gpt-test",
                [AiChatMessage.User("Вопрос")],
                "Системная инструкция",
                Temperature: 0.4,
                MaxTokens: 512),
            CancellationToken.None);

        Assert.Equal("Привет!", response.Text);
        Assert.Equal(12, response.InputTokens);
        Assert.Equal(7, response.OutputTokens);

        Assert.Equal("https://api.example.test/v1/chat/completions", handler.RequestUri);
        Assert.Equal("Bearer sk-test-key", handler.Authorization);
        using var body = System.Text.Json.JsonDocument.Parse(handler.RequestBody!);
        var messages = body.RootElement.GetProperty("messages");
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("Системная инструкция", messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal("Вопрос", messages[1].GetProperty("content").GetString());
        Assert.Equal(0.4, body.RootElement.GetProperty("temperature").GetDouble());
    }

    [Fact]
    public async Task OpenRouter_SendsAttributionHeaders()
    {
        var handler = new FakeHandler(
            HttpStatusCode.OK,
            """{ "choices": [ { "message": { "content": "ok" } } ] }""");

        using var client = new OpenAiCompatibleChatClient(AiProviderKind.OpenRouter, handler);
        var settings = new AiSettings(AiProviderKind.OpenRouter, "openai/gpt-test", BaseUrl: null);

        await client.CompleteAsync(
            settings,
            "sk-or-key",
            new AiChatRequest("openai/gpt-test", [AiChatMessage.User("hi")], null, null, 256),
            CancellationToken.None);

        Assert.Equal("FreeTeachClass", handler.Headers["X-Title"]);
        Assert.False(string.IsNullOrWhiteSpace(handler.Headers["HTTP-Referer"]));
    }

    [Fact]
    public async Task Gemini_UsesApiKeyHeaderAndParsesCandidates()
    {
        var handler = new FakeHandler(
            HttpStatusCode.OK,
            """
            {
              "candidates": [
                { "content": { "parts": [ { "text": "Часть 1. " }, { "text": "Часть 2." } ] } }
              ],
              "usageMetadata": { "promptTokenCount": 9, "candidatesTokenCount": 5 }
            }
            """);

        using var client = new GeminiChatClient(handler);
        var settings = new AiSettings(AiProviderKind.Gemini, "gemini-test", BaseUrl: "https://gemini.example.test/v1beta");

        var response = await client.CompleteAsync(
            settings,
            "gemini-key",
            new AiChatRequest(
                "gemini-test",
                [AiChatMessage.User("Вопрос")],
                "Системная инструкция",
                Temperature: 0.2,
                MaxTokens: 800),
            CancellationToken.None);

        Assert.Equal("Часть 1. Часть 2.", response.Text);
        Assert.Equal(9, response.InputTokens);
        Assert.Equal(5, response.OutputTokens);

        Assert.Equal(
            "https://gemini.example.test/v1beta/models/gemini-test:generateContent",
            handler.RequestUri);
        Assert.Equal("gemini-key", handler.Headers["x-goog-api-key"]);
        Assert.Contains("systemInstruction", handler.RequestBody);
        Assert.Contains("\"role\":\"user\"", handler.RequestBody);
        Assert.Contains("\"maxOutputTokens\":800", handler.RequestBody);
    }

    [Fact]
    public async Task Gemini_ReportsSafetyBlockAsProviderError()
    {
        var handler = new FakeHandler(
            HttpStatusCode.OK,
            """{ "promptFeedback": { "blockReason": "SAFETY" } }""");

        using var client = new GeminiChatClient(handler);
        var settings = new AiSettings(AiProviderKind.Gemini, "gemini-test", BaseUrl: null);

        var exception = await Assert.ThrowsAsync<AiProviderException>(() =>
            client.CompleteAsync(
                settings,
                "gemini-key",
                new AiChatRequest("gemini-test", [AiChatMessage.User("hi")], null, null, 256),
                CancellationToken.None));

        Assert.Equal(AiProviderKind.Gemini, exception.Provider);
        Assert.Equal("SAFETY", exception.ProviderCode);
    }

    [Fact]
    public async Task Anthropic_SendsVersionAndApiKeyHeadersAndParsesContentBlocks()
    {
        var handler = new FakeHandler(
            HttpStatusCode.OK,
            """
            {
              "content": [
                { "type": "text", "text": "Ответ " },
                { "type": "text", "text": "Claude" }
              ],
              "usage": { "input_tokens": 20, "output_tokens": 3 }
            }
            """);

        using var client = new AnthropicChatClient(handler);
        var settings = new AiSettings(AiProviderKind.Anthropic, "claude-test", BaseUrl: "https://anthropic.example.test/v1");

        var response = await client.CompleteAsync(
            settings,
            "sk-ant-key",
            new AiChatRequest(
                "claude-test",
                [AiChatMessage.User("Вопрос")],
                "Системная инструкция",
                Temperature: 0.5,
                MaxTokens: 1024),
            CancellationToken.None);

        Assert.Equal("Ответ Claude", response.Text);
        Assert.Equal(20, response.InputTokens);
        Assert.Equal(3, response.OutputTokens);

        Assert.Equal("https://anthropic.example.test/v1/messages", handler.RequestUri);
        Assert.Equal("sk-ant-key", handler.Headers["x-api-key"]);
        Assert.Equal("2023-06-01", handler.Headers["anthropic-version"]);
        using var body = System.Text.Json.JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("Системная инструкция", body.RootElement.GetProperty("system").GetString());
        Assert.Equal(1024, body.RootElement.GetProperty("max_tokens").GetInt32());
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "invalid_api_key", "Неверный ключ")]
    [InlineData(HttpStatusCode.TooManyRequests, "rate_limit", "Слишком много запросов")]
    public async Task ProviderError_IsMappedAndNeverContainsTheApiKey(
        HttpStatusCode status,
        string code,
        string message)
    {
        var handler = new FakeHandler(
            status,
            $$"""
            { "error": { "type": "{{code}}", "message": "{{message}}" } }
            """);

        using var client = new OpenAiCompatibleChatClient(AiProviderKind.OpenAi, handler);
        var settings = new AiSettings(AiProviderKind.OpenAi, "gpt-test", BaseUrl: null);

        var exception = await Assert.ThrowsAsync<AiProviderException>(() =>
            client.CompleteAsync(
                settings,
                "sk-super-secret",
                new AiChatRequest("gpt-test", [AiChatMessage.User("hi")], null, null, 128),
                CancellationToken.None));

        Assert.Equal(status, exception.StatusCode);
        Assert.Equal(code, exception.ProviderCode);
        Assert.Equal(message, exception.Message);
        Assert.DoesNotContain("sk-super-secret", exception.ToString());
    }

    [Fact]
    public async Task EmptyOpenAiResponse_IsReportedAsProviderError()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, """{ "choices": [] }""");
        using var client = new OpenAiCompatibleChatClient(AiProviderKind.OpenAi, handler);

        var exception = await Assert.ThrowsAsync<AiProviderException>(() =>
            client.CompleteAsync(
                new AiSettings(AiProviderKind.OpenAi, "gpt-test", BaseUrl: null),
                "key",
                new AiChatRequest("gpt-test", [AiChatMessage.User("hi")], null, null, 128),
                CancellationToken.None));

        Assert.Equal("empty_response", exception.ProviderCode);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _responseBody;

        public FakeHandler(HttpStatusCode status, string responseBody)
        {
            _status = status;
            _responseBody = responseBody;
        }

        public string? RequestUri { get; private set; }

        public string? RequestBody { get; private set; }

        public string? Authorization { get; private set; }

        public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri?.ToString();
            Authorization = request.Headers.Authorization?.ToString();

            foreach (var header in request.Headers)
            {
                Headers[header.Key] = string.Join(", ", header.Value);
            }

            if (request.Content is not null)
            {
                RequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json"),
            };
        }
    }
}
