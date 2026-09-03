namespace ArmaReforger.Identity.Bohemia;

/// <summary>
/// Ответ BI Identity API. accessTokenExp — Unix-время в секундах.
/// </summary>
internal sealed record BiAuthResponse(
    string IdentityId,
    string AccessToken,
    long AccessTokenExp);
