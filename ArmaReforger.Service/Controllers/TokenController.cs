using ArmaReforger.Identity.Bohemia;
using ArmaReforger.Service.Contracts;
using ArmaReforger.Service.Tokens;
using Microsoft.AspNetCore.Mvc;

namespace ArmaReforger.Service.Controllers;

[ApiController]
[Route("token")]
public sealed class TokenController : ControllerBase
{
    private readonly IBiTokenStore _tokenStore;

    public TokenController(IBiTokenStore tokenStore)
    {
        _tokenStore = tokenStore;
    }

    /// <summary>
    /// Отдаёт то, что лежит в хранилище. Пусто — 404.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TokenResponse>> Get(CancellationToken cancellationToken)
    {
        var token = await _tokenStore.GetAsync(cancellationToken);

        if (token is null)
        {
            return NotFound();
        }

        return new TokenResponse(token.AccessToken, token.ExpiresAt);
    }
}
