namespace ArmaReforger.Service.Contracts;

/// <summary>
/// Тело ответа GET /token.
/// </summary>
public sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
