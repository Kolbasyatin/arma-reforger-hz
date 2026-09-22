namespace ArmaReforger.Identity.Configuration;

/// <summary>
/// Публичный Steam Web API (профили, друзья, игры, баны).
/// К SteamKit-сессии отношения не имеет: там билет для Bohemia, здесь обычный ключ Valve.
/// </summary>
public sealed class SteamWebApiOptions
{
    public const string SectionName = "SteamWebApi";

    /// <summary>
    /// Ключ с steamcommunity.com/dev/apikey. В appsettings НЕ хранится — только
    /// переменная окружения SteamWebApi__ApiKey. Пусто — эндпоинты профилей отвечают 503.
    /// </summary>
    public string ApiKey { get; init; } = string.Empty;

    public Uri BaseAddress { get; init; } = new("https://api.steampowered.com/");

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);
}
