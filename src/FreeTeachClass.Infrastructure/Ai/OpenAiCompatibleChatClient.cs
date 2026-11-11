using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FreeTeachClass.Application.Ai;
using FreeTeachClass.Domain.Enums;

namespace FreeTeachClass.Infrastructure.Ai;

/// <summary>
/// Adapter for the OpenAI chat-completions shape. It is used for OpenAI,
/// OpenRouter and any user-supplied OpenAI-compatible endpoint.
/// </summary>
public sealed class OpenAiCompatibleChatClient : IAiChatClient, IDisposable
{
    private readonly HttpClient _http;

    public OpenAiCompatibleChatClient(AiProviderKind kind)
        : this(kind, null)
    {
    }

    public OpenAiCompatibleChatClient(AiProviderKind kind, HttpMessageHandler? handler)
    {
        if (kind is AiProviderKind.Gemini or AiProviderKind.Anthropic)
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Этот адаптер не поддерживает выбранный тип провайдера.");
        }

        Kind = kind;
        _http = AiHttp.CreateClient(handler);
    }

    public AiProviderKind Kind { get; }

    public async Task<AiChatResponse> CompleteAsync(
        AiSettings settings,
        string apiKey,
        AiChatRequest request,
        CancellationToken cancellationToken)
    {
        var baseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl)
            ? AiProviderCatalog.Get(Kind).DefaultBaseUrl
            : settings.BaseUrl;

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new AiProviderException(Kind, null, "missing_base_url", "Не задан адрес API провайдера.");
        }

        var url = $"{baseUrl.TrimEnd('/')}/chat/completions";
        var payload = BuildPayload(request);

        using var message = new HttpRequestMessage(HttpMethod.Post, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (Kind == AiProviderKind.OpenRouter)
        {
            // Optional attribution headers recommended by OpenRouter.
            message.Headers.TryAddWithoutValidation("HTTP-Referer", "https://github.com/freeteachclass");
            message.Headers.TryAddWithoutValidation("X-Title", "FreeTeachClass");
        }

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
        var text = ReadContent(root);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw AiHttp.EmptyResponse(Kind, response.StatusCode);
        }

        var (inputTokens, outputTokens) = ReadUsage(root);
        return new AiChatResponse(text.Trim(), inputTokens, outputTokens);
    }

    public void Dispose() => _http.Dispose();

    private static Dictionary<string, object> BuildPayload(AiChatRequest request)
    {
        var messages = new List<Dictionary<string, string>>();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            messages.Add(new Dictionary<string, string>
            {
                ["role"] = "system",
                ["content"] = request.SystemPrompt,
            });
        }

        messages.AddRange(request.Messages.Select(message => new Dictionary<string, string>
        {
            ["role"] = message.Role,
            ["content"] = message.Content,
        }));

        var payload = new Dictionary<string, object>
        {
            ["model"] = request.Model,
            ["messages"] = messages,
        };

        if (request.Temperature is { } temperature)
        {
            payload["temperature"] = temperature;
        }

        return payload;
    }

    private static string? ReadContent(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices)
            || choices.ValueKind != JsonValueKind.Array
            || choices.GetArrayLength() == 0)
        {
            return null;
        }

        var first = choices[0];
        if (!first.TryGetProperty("message", out var message)
            || !message.TryGetProperty("content", out var content))
        {
            return null;
        }

        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString();
        }

        // Some OpenAI-compatible gateways return an array of content parts.
        if (content.ValueKind == JsonValueKind.Array)
        {
            var builder = new StringBuilder();
            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                {
                    builder.Append(text.GetString());
                }
            }

            return builder.ToString();
        }

        return null;
    }

    private static (int? Input, int? Output) ReadUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
        {
            return (null, null);
        }

        return (
            ReadInt(usage, "prompt_tokens"),
            ReadInt(usage, "completion_tokens"));
    }

    private static int? ReadInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : null;
}
