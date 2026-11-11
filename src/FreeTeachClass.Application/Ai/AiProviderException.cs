using System.Net;
using FreeTeachClass.Domain.Enums;

namespace FreeTeachClass.Application.Ai;

/// <summary>
/// A provider returned an error. The message is deliberately sanitised: it
/// contains the provider's error text, but never request headers or API keys.
/// </summary>
public sealed class AiProviderException : Exception
{
    public AiProviderException(
        AiProviderKind provider,
        HttpStatusCode? statusCode,
        string? providerCode,
        string message)
        : base(message)
    {
        Provider = provider;
        StatusCode = statusCode;
        ProviderCode = providerCode;
    }

    public AiProviderKind Provider { get; }

    public HttpStatusCode? StatusCode { get; }

    public string? ProviderCode { get; }
}
