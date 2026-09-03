namespace ArmaReforger.Identity.Steam;

/// <summary>
/// Хранилище состояния Steam-аутентификации.
/// Сервис только читает; записывает интерактивный инструмент первого входа.
/// </summary>
public interface ISteamAuthStateStore
{
    ValueTask<SteamAuthState?> LoadAsync(CancellationToken cancellationToken = default);

    ValueTask SaveAsync(SteamAuthState state, CancellationToken cancellationToken = default);
}
