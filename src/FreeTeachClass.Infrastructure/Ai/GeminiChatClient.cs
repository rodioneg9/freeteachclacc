using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FreeTeachClass.Application.Ai;
using FreeTeachClass.Domain.Enums;

namespace FreeTeachClass.Infrastructure.Ai;

/// <summary>Google Gemini adapter (generateContent).</summary>
public sealed class GeminiChatClient : IAiChatClient, IDisposable
{
    private readonly HttpClient _http;

    public GeminiChatClient()
        : this(null)
    {
    }

    public GeminiChatClient(HttpMessageHandler? handler)
    {
        _http = AiHttp.CreateClient(handler);
    }

    public AiProviderKind Kind => AiProviderKind.Gemini;

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

        var url = $"{baseUrl}/models/{Uri.EscapeDataString(request.Model)}:generateContent";

        var contents = request.Messages.Select(message => new Dictionary<string, object>
        {
            ["role"] = message.Role == AiChatMessage.AssistantRole ? "model" : "user",
            ["parts"] = new[] { new Dictionary<string, string> { ["text"] = message.Content } },
        }).ToArray();

        var payload = new Dictionary<string, object>
        {
            ["contents"] = contents,
            ["generationConfig"] = new Dictionary<string, object?>
            {
                ["temperature"] = request.Temperature,
                ["maxOutputTokens"] = request.MaxTokens,
            },
        };

        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            payload["systemInstruction"] = new Dictionary<string, object>
            {
                ["parts"] = new[] { new Dictionary<string, string> { ["text"] = request.SystemPrompt } },
            };
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, url);
        message.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
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
            var blockReason = ReadBlockReason(root);
            throw blockReason is null
                ? AiHttp.EmptyResponse(Kind, response.StatusCode)
                : new AiProviderException(
                    Kind,
                    response.StatusCode,
                    blockReason,
                    $"Запрос заблокирован фильтром безопасности Gemini ({blockReason}).");
        }

        var (inputTokens, outputTokens) = ReadUsage(root);
        return new AiChatResponse(text.Trim(), inputTokens, outputTokens);
    }

    public void Dispose() => _http.Dispose();

    private static string? ReadText(JsonElement root)
    {
        if (!root.TryGetProperty("candidates", out var candidates)
            || candidates.ValueKind != JsonValueKind.Array
            || candidates.GetArrayLength() == 0)
        {
            return null;
        }

        if (!candidates[0].TryGetProperty("content", out var content)
            || !content.TryGetProperty("parts", out var parts)
            || parts.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var builder = new StringBuilder();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
            {
                builder.Append(text.GetString());
            }
        }

        return builder.ToString();
    }

    private static string? ReadBlockReason(JsonElement root)
    {
        if (root.TryGetProperty("promptFeedback", out var feedback)
            && feedback.TryGetProperty("blockReason", out var reason)
            && reason.ValueKind == JsonValueKind.String)
        {
            return reason.GetString();
        }

        return null;
    }

    private static (int? Input, int? Output) ReadUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usageMetadata", out var usage) || usage.ValueKind != JsonValueKind.Object)
        {
            return (null, null);
        }

        return (
            ReadInt(usage, "promptTokenCount"),
            ReadInt(usage, "candidatesTokenCount"));
    }

    private static int? ReadInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : null;
}
