namespace FreeTeachClass.Infrastructure.Google;

/// <summary>Thrown when OAuth client credentials are not configured on this machine.</summary>
public sealed class MissingOAuthConfigurationException : Exception
{
    public MissingOAuthConfigurationException()
        : base(
            "Не настроены учётные данные Google OAuth. Задайте переменную окружения " +
            $"{GoogleOAuthOptions.ClientIdEnvironmentVariable} или файл конфигурации " +
            "%LOCALAPPDATA%\\FreeTeachClass\\oauth-client.json, либо впишите Client ID и " +
            "Client Secret в GoogleOAuthDefaults (src/FreeTeachClass.Infrastructure/Google). " +
            "Инструкция — в README.md.")
    {
    }
}
