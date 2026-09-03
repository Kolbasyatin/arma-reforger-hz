
namespace ArmaReforger.Identity.Bohemia;

/// <summary>
/// Обменивает Steam Web API билет на BI access token.
/// </summary>
public interface IBiIdentityClient
{
    Task<BiToken> AuthenticateAsync(
        string steamTicketBase64,
        CancellationToken cancellationToken = default);
}
