using System.Text.Json;
using ArmaReforger.Identity.Configuration;
using Microsoft.Extensions.Options;

namespace ArmaReforger.Identity.Steam;

/// <summary>
/// Публичный Steam Web API. Задача одна — сходить и принести сырой JSON.
///
/// Источники запрашиваются ПАРАЛЛЕЛЬНО и независимо: у закрытого профиля друзья и игры
/// недоступны, а сводка и баны приходят всегда. Если бы один отказ ронял весь запрос,
/// по половине игроков мы не получили бы вообще ничего.
/// </summary>
public sealed class SteamProfileClient : ISteamProfileClient
{
    // Путь и набор параметров у каждого метода свой, общее только ключ и steamid.
    private static readonly (string Source, string Path, string IdParameter)[] Endpoints =
    [
        ("summary", "ISteamUser/GetPlayerSummaries/v2/", "steamids"),
        ("friends", "ISteamUser/GetFriendList/v1/", "steamid"),
        ("games", "IPlayerService/GetOwnedGames/v1/", "steamid"),
        ("bans", "ISteamUser/GetPlayerBans/v1/", "steamids"),
    ];

    private readonly HttpClient _httpClient;
    private readonly SteamWebApiOptions _options;
    private readonly ILogger<SteamProfileClient> _logger;

    public SteamProfileClient(
        HttpClient httpClient,
        IOptions<SteamWebApiOptions> options,
        ILogger<SteamProfileClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SteamProfileSnapshot> FetchAsync(string steamId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new SteamWebApiNotConfiguredException();
        }

        var fetchedAt = DateTimeOffset.UtcNow;

        // Task.WhenAll, а не последовательный цикл: четыре независимых запроса к одному хосту,
        // ждать их по очереди — значит вчетверо дольше на ровном месте.
        var sources = await Task.WhenAll(
            Endpoints.Select(endpoint => FetchOneAsync(endpoint, steamId, cancellationToken)));

        return new SteamProfileSnapshot(steamId, fetchedAt, sources);
    }

    private async Task<SteamSource> FetchOneAsync(
        (string Source, string Path, string IdParameter) endpoint,
        string steamId,
        CancellationToken cancellationToken)
    {
        // GetOwnedGames без этих двух флагов возвращает только appid и время — без названий игр.
        var extra = endpoint.Source == "games"
            ? "&include_appinfo=1&include_played_free_games=1"
            : string.Empty;

        var path = $"{endpoint.Path}?key={_options.ApiKey}&{endpoint.IdParameter}={steamId}{extra}";

        try
        {
            using var response = await _httpClient.GetAsync(path, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // 401/403 у друзей и игр — закрытый профиль. Пишем как есть, без интерпретации:
                // отличить «закрыт» от «ключ отозван» по коду нельзя, а врать в лог не стоит.
                _logger.LogInformation(
                    "Steam {Source} for {SteamId}: HTTP {Status}",
                    endpoint.Source, steamId, (int)response.StatusCode);

                return new SteamSource(endpoint.Source, null, $"HTTP {(int)response.StatusCode}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            // Clone обязателен: JsonDocument владеет буфером и освободит его на выходе из using,
            // а JsonElement на освобождённый буфер бросит ObjectDisposedException при первом чтении.
            return new SteamSource(endpoint.Source, document.RootElement.Clone(), null);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            _logger.LogWarning(exception, "Steam {Source} for {SteamId} failed", endpoint.Source, steamId);

            return new SteamSource(endpoint.Source, null, exception.GetType().Name);
        }
    }
}

/// <summary>Ключ не задан: это ошибка настройки, а не запроса, поэтому отдельный тип.</summary>
public sealed class SteamWebApiNotConfiguredException : Exception
{
    public SteamWebApiNotConfiguredException()
        : base("SteamWebApi:ApiKey is not configured")
    {
    }
}
