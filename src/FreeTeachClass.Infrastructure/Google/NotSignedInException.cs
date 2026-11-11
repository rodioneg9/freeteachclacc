namespace FreeTeachClass.Infrastructure.Google;

/// <summary>Thrown when an API call is attempted without a usable Google session.</summary>
public sealed class NotSignedInException : Exception
{
    public NotSignedInException()
        : base("Нет активной сессии Google. Войдите в аккаунт и повторите попытку.")
    {
    }
}
