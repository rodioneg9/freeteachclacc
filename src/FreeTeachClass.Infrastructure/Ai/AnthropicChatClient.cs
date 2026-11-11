using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FreeTeachClass.Application.Ai;
using FreeTeachClass.Domain.Enums;

namespace FreeTeachClass.Infrastructure.Ai;

/// <summary>Anthropic Messages API adapter.</summary>
public sealed class AnthropicChatClient : IAiChatClient, IDisposable
{
    private const string AnthropicVersion = "2023-06-01";

    private readonly HttpClient _http;

    public AnthropicChatClient()
        : this(null)
    {
    }

    public AnthropicChatClient(HttpMessageHandler? handler)
    {
        _http = AiHttp.CreateClient(handler);
    }

    public AiProviderKind Kind => AiProviderKind.Anthropic;

    public async Task<AiChatResponse> CompleteAsync(
        AiSettings settings,
        string apiKey,
        AiChatRequest request,
        CancellationToken cancellationToken)
    {
        var baseUrl = (string.IsNullOrWhiteSpace(settings.BaseUrl)
                ? AiProviderCatalog.Get(Kind).DefaultBaseUrl
                : settings.BaseUrl)!
            .TrimEnd('/');

        var url = $"{baseUrl}/messages";

        var messages = request.Messages.Select(message => new Dictionary<string, string>
        {
            ["role"] = message.Role == AiChatMessage.AssistantRole ? "assistant" : "user",
            ["content"] = message.Content,
        }).ToArray();

        var payload = new Dictionary<string, object>
        {
            ["model"] = request.Model,
            ["max_tokens"] = request.MaxTokens,
            ["messages"] = messages,
        };

        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            payload["system"] = request.SystemPrompt;
        }

        if (request.Temperature is { } temperature)
        {
            payload["temperature"] = temperature;
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, url);
        message.Headers.TryAddWithoutValidation("x-api-key", apiKey);
        message.Headers.TryAddWithoutValidation("anthropic-version", AnthropicVersion);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        message.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using var response = await _http
            .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        await AiHttp.EnsureSuccessAsync(response, Kind, cancellationToken).ConfigureAwait(false);

        await using var stream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var document = await JsonDocument
            .ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var root = document.RootElement;
        var text = ReadText(root);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw AiHttp.EmptyResponse(Kind, response.StatusCode);
        }

        var (inputTokens, outputTokens) = ReadUsage(root);
        return new AiChatResponse(text.Trim(), inputTokens, outputTokens);
    }

    public void Dispose() => _http.Dispose();

    private static string? ReadText(JsonElement root)
    {
        if (!root.TryGetProperty("content", out var content)
            || content.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var builder = new StringBuilder();
        foreach (var block in content.EnumerateArray())
        {
            if (block.TryGetProperty("type", out var type)
                && type.ValueKind == JsonValueKind.String
                && type.GetString() == "text"
                && block.TryGetProperty("text", out var text)
                && text.ValueKind == JsonValueKind.String)
            {
                builder.Append(text.GetString());
            }
        }

        return builder.ToString();
    }

    private static (int? Input, int? Output) ReadUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
        {
            return (null, null);
        }

        return (
            ReadInt(usage, "input_tokens"),
            ReadInt(usage, "output_tokens"));
    }

    private static int? ReadInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : null;
}
