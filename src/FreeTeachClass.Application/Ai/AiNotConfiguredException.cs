namespace FreeTeachClass.Application.Ai;

/// <summary>Raised when no API key is stored for the selected provider.</summary>
public sealed class AiNotConfiguredException : Exception
{
    public AiNotConfiguredException(string providerName)
        : base($"Для {providerName} не сохранён API-ключ. Добавьте его в настройках AI.")
    {
    }
}
