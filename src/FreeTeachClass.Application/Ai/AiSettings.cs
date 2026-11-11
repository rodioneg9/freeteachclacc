using FreeTeachClass.Domain.Enums;

namespace FreeTeachClass.Application.Ai;

/// <summary>
/// Non-secret AI configuration. API keys are stored separately by
/// <see cref="Abstractions.IAiKeyStore"/> and are never part of this record.
/// </summary>
public sealed record AiSettings(
    AiProviderKind Provider,
    string Model,
    string? BaseUrl,
    double Temperature = 0.7,
    int MaxTokens = 2048);
