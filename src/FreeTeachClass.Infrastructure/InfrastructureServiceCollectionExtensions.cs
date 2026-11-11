using System.Runtime.Versioning;
using FreeTeachClass.Application.Abstractions;
using FreeTeachClass.Application.Ai;
using FreeTeachClass.Domain.Abstractions;
using FreeTeachClass.Domain.Enums;
using FreeTeachClass.Infrastructure.Ai;
using FreeTeachClass.Infrastructure.Google;
using FreeTeachClass.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace FreeTeachClass.Infrastructure;

/// <summary>
/// Windows-only composition root for Infrastructure (DPAPI token storage).
/// </summary>
[SupportedOSPlatform("windows")]
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers Infrastructure implementations of the Domain/Application ports.
    /// OAuth credentials are loaded from the environment or a local config file —
    /// never from source code.
    /// </summary>
    public static IServiceCollection AddFreeTeachClassInfrastructure(this IServiceCollection services)
    {
        // Empty options (ClientId = "") mean "not configured"; the gateway
        // then throws MissingOAuthConfigurationException at sign-in time.
        services.AddSingleton(_ =>
        {
            GoogleOAuthOptions.TryLoad(out var options);
            return options ?? new GoogleOAuthOptions();
        });

        services.AddSingleton<DpapiTokenStore>();
        services.AddSingleton<ISecureTokenStore>(sp => sp.GetRequiredService<DpapiTokenStore>());

        services.AddSingleton<AuthStateStore>();
        services.AddSingleton<DpapiAiKeyStore>();
        services.AddSingleton<IAiKeyStore>(sp => sp.GetRequiredService<DpapiAiKeyStore>());
        services.AddSingleton<IAiSettingsStore, JsonAiSettingsStore>();

        services.AddSingleton<IAiChatClient>(_ => new OpenAiCompatibleChatClient(AiProviderKind.OpenAi));
        services.AddSingleton<IAiChatClient>(_ => new OpenAiCompatibleChatClient(AiProviderKind.OpenRouter));
        services.AddSingleton<IAiChatClient>(_ => new OpenAiCompatibleChatClient(AiProviderKind.CustomOpenAiCompatible));
        services.AddSingleton<IAiChatClient, GeminiChatClient>();
        services.AddSingleton<IAiChatClient, AnthropicChatClient>();

        services.AddSingleton<JsonLocalDataStore>();
        services.AddSingleton<ILocalDataStore>(sp => sp.GetRequiredService<JsonLocalDataStore>());

        services.AddSingleton<GoogleAuthGateway>();
        services.AddSingleton<IGoogleAuthGateway>(sp => sp.GetRequiredService<GoogleAuthGateway>());
        services.AddSingleton<IGoogleCredentialProvider>(sp => sp.GetRequiredService<GoogleAuthGateway>());

        services.AddSingleton<IClassroomDataSource, GoogleClassroomDataSource>();
        services.AddSingleton<ISubmissionGateway, GoogleSubmissionGateway>();

        return services;
    }
}
