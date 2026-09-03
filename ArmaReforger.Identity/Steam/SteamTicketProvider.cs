using ArmaReforger.Identity.Configuration;
using Microsoft.Extensions.Options;

namespace ArmaReforger.Identity.Steam;

public sealed class SteamTicketProvider : ISteamTicketProvider
{
    private readonly ISteamAuthStateStore _authStateStore;
    private readonly SteamOptions _options;
    private readonly ILogger<SteamTicketProvider> _logger;

    public SteamTicketProvider(
        ISteamAuthStateStore authStateStore,
        IOptions<SteamOptions> options,
        ILogger<SteamTicketProvider> logger)
    {
        _authStateStore = authStateStore;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SteamWebApiTicket> AcquireAsync(
        CancellationToken cancellationToken = default)
    {
        var authState = await _authStateStore.LoadAsync(cancellationToken)
                        ?? throw new SteamSessionException(
                            "No saved Steam auth state. Run the interactive login tool first.");

        var connection = await SteamConnection.ConnectAsync(
            _options.OperationTimeout,
            _logger,
            cancellationToken);

        try
        {
            await connection.LogOnAsync(authState, _options.OperationTimeout, cancellationToken);

            var ticketInfo = await connection.AuthTicket.GetAuthTicketForWebApi(
                _options.AppId,
                _options.TicketIdentity);

            try
            {
                var ticket = new SteamWebApiTicket(
                    ticketInfo,
                    connection,
                    WebApiTicketReader.Trim(ticketInfo.Ticket));

                _logger.LogInformation(
                    "Web API ticket acquired: {ActualLength} of {BufferLength} bytes",
                    ticket.ActualLength,
                    ticket.BufferLength);

                return ticket;
            }
            catch
            {
                ticketInfo.Dispose();

                throw;
            }
        }
        catch
        {
            await connection.DisposeAsync();

            throw;
        }
    }
}
