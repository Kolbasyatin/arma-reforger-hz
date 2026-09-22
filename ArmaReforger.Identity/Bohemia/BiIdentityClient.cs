using System.Net.Http.Json;
using System.Text.Json;
using ArmaReforger.Identity.Configuration;
using Microsoft.Extensions.Options;

namespace ArmaReforger.Identity.Bohemia;

public sealed class BiIdentityClient : IBiIdentityClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly SteamOptions _steamOptions;
    private readonly BohemiaOptions _bohemiaOptions;
    private readonly ILogger<BiIdentityClient> _logger;

    public BiIdentityClient(
        HttpClient httpClient,
        IOptions<SteamOptions> steamOptions,
        IOptions<BohemiaOptions> bohemiaOptions,
        ILogger<BiIdentityClient> logger)
    {
        _httpClient = httpClient;
        _steamOptions = steamOptions.Value;
        _bohemiaOptions = bohemiaOptions.Value;
        _logger = logger;
    }

    public async Task<BiToken> AuthenticateAsync(
        string steamTicketBase64,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            platform = "steam",
            token = steamTicketBase64,
            platformOpts = new
            {
                appId = _steamOptions.AppId.ToString()
            }
        };

        using var response = await _httpClient.PostAsJsonAsync(
            _bohemiaOptions.AuthPath,
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);

            throw new BiAuthenticationException(response.StatusCode, errorBody);
        }

        var payload = await response.Content.ReadFromJsonAsync<BiAuthResponse>(
                          SerializerOptions,
                          cancellationToken)
                      ?? throw new BiAuthenticationException(
                          response.StatusCode,
                          "Empty BI authentication response");

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(payload.AccessTokenExp);

        _logger.LogInformation("BI access token received, expires at {ExpiresAt:O}", expiresAt);

        return new BiToken(payload.AccessToken, expiresAt);
    }
}
