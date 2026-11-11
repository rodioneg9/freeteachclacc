namespace FreeTeachClass.Application.Ai;

/// <summary>How a provider is presented in the settings UI.</summary>
public sealed record AiProviderInfo(
    Domain.Enums.AiProviderKind Kind,
    string Name,
    string DefaultModel,
    IReadOnlyList<string> Models,
    string? DefaultBaseUrl,
    string KeyHint,
    string? KeyUrl,
    bool SupportsCustomBaseUrl);
