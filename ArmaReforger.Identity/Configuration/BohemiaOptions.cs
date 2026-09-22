namespace ArmaReforger.Identity.Configuration;

/// <summary>
/// Настройки BI Identity API — обмен Steam-билета на access token.
/// Game API (lobby, session) — зона другого сервиса, здесь его нет.
/// </summary>
public sealed class BohemiaOptions
{
    public const string SectionName = "Bohemia";

    public Uri IdentityBaseAddress { get; init; } = new("https://api-ar-id.bistudio.com/");

    /// <summary>BI проверяет User-Agent; значение меняется с версией игры.</summary>
    public string UserAgent { get; init; } = "Arma Reforger/1.8.0.10 (Client; Windows)";

    /// <summary>
    /// Путь обмена билета на токен. В конфиге, а не константой в коде: BI версионирует API
    /// сама по себе, и подобрать рабочий путь на проде должно быть можно перезапуском,
    /// а не пересборкой образа.
    /// </summary>
    public string AuthPath { get; init; } =
        "game-identity/api/v1.1/identities/reforger/auth?include=profile";
}
