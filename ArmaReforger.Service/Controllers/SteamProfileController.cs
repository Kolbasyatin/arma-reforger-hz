using ArmaReforger.Identity.Steam;
using Microsoft.AspNetCore.Mvc;

namespace ArmaReforger.Service.Controllers;

/// <summary>
/// Данные Steam по SteamID64. Отдаёт сырые ответы Valve, ничего не сохраняя:
/// хранение — забота armaplayers, здесь только шлюз, как и для токена.
/// </summary>
[ApiController]
[Route("steam")]
public sealed class SteamProfileController : ControllerBase
{
    private readonly ISteamProfileClient _client;

    public SteamProfileController(ISteamProfileClient client)
    {
        _client = client;
    }

    [HttpGet("{steamId}")]
    [ProducesResponseType<SteamProfileSnapshot>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SteamProfileSnapshot>> Get(string steamId, CancellationToken cancellationToken)
    {
        // SteamID64 — 17 цифр. Проверяем до похода наружу: чужой API на мусор ответит 200
        // с пустым списком, и отличить «нет такого» от «неверный запрос» станет невозможно.
        if (steamId.Length != 17 || !steamId.All(char.IsAsciiDigit))
        {
            return BadRequest(new {error = "steamId must be a 17-digit SteamID64"});
        }

        try
        {
            return await _client.FetchAsync(steamId, cancellationToken);
        }
        catch (SteamWebApiNotConfiguredException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new {error = "Steam Web API key is not configured"});
        }
    }
}
