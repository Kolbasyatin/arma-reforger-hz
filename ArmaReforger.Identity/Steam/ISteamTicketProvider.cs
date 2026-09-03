namespace ArmaReforger.Identity.Steam;

/// <summary>
/// Выдаёт свежий Steam Web API билет для обмена в BI.
/// </summary>
public interface ISteamTicketProvider
{
    /// <summary>
    /// Подключается к Steam, входит по сохранённому refresh token и берёт билет.
    /// Возвращённую аренду нужно освободить после обмена билета.
    /// </summary>
    Task<SteamWebApiTicket> AcquireAsync(CancellationToken cancellationToken = default);
}
