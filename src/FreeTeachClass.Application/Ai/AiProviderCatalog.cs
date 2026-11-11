using FreeTeachClass.Domain.Enums;

namespace FreeTeachClass.Application.Ai;

/// <summary>
/// Static description of the supported providers. Model names are suggestions:
/// the UI allows typing any model identifier, because providers rename models
/// far more often than this application is updated.
/// </summary>
public static class AiProviderCatalog
{
    public static IReadOnlyList<AiProviderInfo> All { get; } =
    [
        new(
            AiProviderKind.OpenAi,
            "OpenAI",
            DefaultModel: "gpt-4.1-mini",
            Models:
            [
                "gpt-4.1-mini",
                "gpt-4.1",
                "gpt-4o-mini",
                "o4-mini",
            ],
            DefaultBaseUrl: "https://api.openai.com/v1",
            KeyHint: "Ключ начинается с «sk-». Создать: platform.openai.com/api-keys",
            KeyUrl: "https://platform.openai.com/api-keys",
            SupportsCustomBaseUrl: false),

        new(
            AiProviderKind.OpenRouter,
            "OpenRouter",
            DefaultModel: "openai/gpt-4.1-mini",
            Models:
            [
                "openai/gpt-4.1-mini",
                "anthropic/claude-sonnet-4.5",
                "google/gemini-2.5-flash",
                "meta-llama/llama-3.3-70b-instruct",
            ],
            DefaultBaseUrl: "https://openrouter.ai/api/v1",
            KeyHint: "Ключ начинается с «sk-or-». Создать: openrouter.ai/keys",
            KeyUrl: "https://openrouter.ai/keys",
            SupportsCustomBaseUrl: false),

        new(
            AiProviderKind.Gemini,
            "Google Gemini",
            DefaultModel: "gemini-2.5-flash",
            Models:
            [
                "gemini-2.5-flash",
                "gemini-2.5-pro",
                "gemini-2.0-flash",
            ],
            DefaultBaseUrl: "https://generativelanguage.googleapis.com/v1beta",
            KeyHint: "Ключ из Google AI Studio: aistudio.google.com/apikey",
            KeyUrl: "https://aistudio.google.com/apikey",
            SupportsCustomBaseUrl: false),

        new(
            AiProviderKind.Anthropic,
            "Anthropic Claude",
            DefaultModel: "claude-sonnet-4-5",
            Models:
            [
                "claude-sonnet-4-5",
                "claude-haiku-4-5",
                "claude-opus-4-1",
            ],
            DefaultBaseUrl: "https://api.anthropic.com/v1",
            KeyHint: "Ключ начинается с «sk-ant-». Создать: console.anthropic.com",
            KeyUrl: "https://console.anthropic.com/settings/keys",
            SupportsCustomBaseUrl: false),

        new(
            AiProviderKind.CustomOpenAiCompatible,
            "Свой (OpenAI-совместимый)",
            DefaultModel: "gpt-4o-mini",
            Models:
            [
                "gpt-4o-mini",
            ],
            DefaultBaseUrl: "http://localhost:1234/v1",
            KeyHint: "Ключ вашего OpenAI-совместимого сервиса (может быть пустым)",
            KeyUrl: null,
            SupportsCustomBaseUrl: true),
    ];

    public static AiProviderInfo Get(AiProviderKind kind) =>
        All.FirstOrDefault(info => info.Kind == kind) ?? All[0];

    /// <summary>Fills in defaults for missing model/base URL and clamps limits.</summary>
    public static AiSettings Normalize(AiSettings settings)
    {
        var info = Get(settings.Provider);
        var model = string.IsNullOrWhiteSpace(settings.Model) ? info.DefaultModel : settings.Model.Trim();
        var baseUrl = info.SupportsCustomBaseUrl
            ? NullIfWhiteSpace(settings.BaseUrl) ?? info.DefaultBaseUrl
            : info.DefaultBaseUrl;

        return settings with
        {
            Model = model,
            BaseUrl = baseUrl,
            Temperature = Math.Clamp(settings.Temperature, 0, 2),
            MaxTokens = Math.Clamp(settings.MaxTokens, 256, 8192),
        };
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
