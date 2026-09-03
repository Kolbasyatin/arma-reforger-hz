namespace ArmaReforger.Identity.Steam;

/// <summary>Ошибка подключения к Steam или входа в аккаунт.</summary>
public sealed class SteamSessionException : Exception
{
    public SteamSessionException(string message) : base(message)
    {
    }
}
