using System.Net;
using FreeTeachClass.Application.Ai;
using FreeTeachClass.Infrastructure.Google;
using Google;

namespace FreeTeachClass.App;

/// <summary>
/// Maps technical exceptions to short, human-readable Russian messages.
/// Tokens and personal data are never included.
/// </summary>
public static class ErrorMessages
{
    public static string For(Exception exception) => exception switch
    {
        GoogleApiException apiException => ForApiException(apiException),
        AiNotConfiguredException => exception.Message,
        AiProviderException aiException => ForAiException(aiException),
        HttpRequestException => "Нет связи с сервисом. Проверьте подключение к интернету.",
        TaskCanceledException => "Запрос превысил время ожидания. Попробуйте ещё раз.",
        MissingOAuthConfigurationException or NotSignedInException => exception.Message,
        _ => "Произошла непредвиденная ошибка. Подробности — в журнале приложения.",
    };

    private static string ForAiException(AiProviderException exception)
    {
        var status = exception.StatusCode;
        return status switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                "AI-провайдер отклонил API-ключ. Проверьте ключ и доступ к выбранной модели.",
            HttpStatusCode.NotFound =>
                "Выбранная модель не найдена у провайдера. Проверьте название модели в настройках AI.",
            HttpStatusCode.TooManyRequests =>
                "AI-провайдер ограничил частоту запросов. Подождите немного и попробуйте снова.",
            HttpStatusCode.BadRequest =>
                $"AI-провайдер отклонил запрос: {exception.Message}",
            { } code when (int)code >= 500 =>
                "AI-провайдер временно недоступен. Попробуйте позже.",
            _ => $"Ошибка AI-провайдера: {exception.Message}",
        };
    }

    private static string ForApiException(GoogleApiException exception) =>
        (int)exception.HttpStatusCode switch
        {
            401 => "Сессия Google истекла. Войдите заново.",
            403 => "Google отклонил запрос: не хватает разрешений либо аккаунт " +
                   "ограничен политикой Google Workspace.",
            404 => "Данные не найдены. Возможно, курс был архивирован.",
            429 => "Слишком много запросов к Google. Подождите немного и обновите данные.",
            >= 500 => "Сервис Google Classroom временно недоступен. Попробуйте позже.",
            _ => $"Google вернул ошибку ({(int)exception.HttpStatusCode}).",
        };
}
