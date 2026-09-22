namespace ArmaReforger.Identity.Steam;

public interface ISteamProfileClient
{
    /// <summary>
    /// Собирает всё, что Valve отдаёт по одному SteamID64. Не бросает из-за отдельного
    /// неудачного источника: закрытый профиль — это ответ, а не ошибка.
    /// </summary>
    Task<SteamProfileSnapshot> FetchAsync(string steamId, CancellationToken cancellationToken = default);
}
