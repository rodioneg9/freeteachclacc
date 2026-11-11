using System.Net;
using System.Text.Json;
using FreeTeachClass.Application.Ai;
using FreeTeachClass.Domain.Enums;

namespace FreeTeachClass.Infrastructure.Ai;

/// <summary>Small helpers shared by the provider adapters.</summary>
internal static class AiHttp
{
    private const int MaxErrorChars = 300;

    public static HttpClient CreateClient(HttpMessageHandler? handler = null) =>
        new(handler ?? new HttpClientHandler(), disposeHandler: false)
        {
            Timeout = TimeSpan.FromSeconds(90),
        };

    public static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        AiProviderKind provider,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var (code, message) = await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false);
        throw new AiProviderException(provider, response.StatusCode, code, message);
    }

    private static async Task<(string? Code, string Message)> ReadErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        string body;
        try
        {
            body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            body = string.Empty;
        }

        string? code = null;
        string? message = null;

        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.TryGetProperty("error", out var error)
                    && error.ValueKind == JsonValueKind.Object)
                {
                    code = ReadString(error, "status") ?? ReadString(error, "type") ?? ReadString(error, "code");
                    message = ReadString(error, "message");
                }
            }
            catch (JsonException)
            {
                // Not JSON: never echo the whole body (it may contain request echo).
                message = null;
            }
        }

        message = string.IsNullOrWhiteSpace(message)
            ? $"Провайдер вернул ошибку {(int)response.StatusCode} ({response.ReasonPhrase})."
            : Truncate(message);

        return (code, message);
    }

    private static string? ReadString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            _ => null,
        };
    }

    private static string Truncate(string value) =>
        value.Length <= MaxErrorChars ? value : value[..MaxErrorChars] + "…";

    public static AiProviderException EmptyResponse(AiProviderKind provider, HttpStatusCode status) =>
        new(provider, status, "empty_response", "Провайдер вернул пустой ответ.");
}
