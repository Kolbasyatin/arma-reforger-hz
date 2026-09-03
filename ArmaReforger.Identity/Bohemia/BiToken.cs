namespace ArmaReforger.Identity.Bohemia;

/// <summary>
/// BI access token и момент его истечения.
/// Это только данные: решение об обновлении принимает тот,
/// кто токен добывает, а не тот, кто его читает.
/// </summary>
public sealed record BiToken(string AccessToken, DateTimeOffset ExpiresAt);
