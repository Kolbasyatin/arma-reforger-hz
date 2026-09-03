using SteamKit2;

namespace ArmaReforger.Identity.Steam;

/// <summary>
/// Аренда Steam Web API билета: пока объект не освобождён, билет действителен
/// и Steam-соединение живо. Освобождать только после обмена билета в BI.
/// </summary>
public sealed class SteamWebApiTicket : IAsyncDisposable
{
    private readonly SteamAuthTicket.TicketInfo _ticketInfo;
    private readonly SteamConnection _connection;

    internal SteamWebApiTicket(
        SteamAuthTicket.TicketInfo ticketInfo,
        SteamConnection connection,
        ReadOnlySpan<byte> actualTicket)
    {
        _ticketInfo = ticketInfo;
        _connection = connection;

        Base64 = Convert.ToBase64String(actualTicket);
        BufferLength = ticketInfo.Ticket.Length;
        ActualLength = actualTicket.Length;
    }

    /// <summary>Фактические байты билета в Base64 — это уходит в BI.</summary>
    public string Base64 { get; }

    /// <summary>Размер буфера, выданного SteamKit.</summary>
    public int BufferLength { get; }

    /// <summary>Размер настоящей структуры билета.</summary>
    public int ActualLength { get; }

    public async ValueTask DisposeAsync()
    {
        // Dispose отменяет билет в Steam, поэтому идёт до закрытия соединения.
        _ticketInfo.Dispose();

        await _connection.DisposeAsync();
    }
}
