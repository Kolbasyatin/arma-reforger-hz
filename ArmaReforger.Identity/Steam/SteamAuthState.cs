namespace ArmaReforger.Identity.Steam;

/// <summary>
/// Долгоживущее состояние Steam-аутентификации.
/// RefreshToken позволяет входить без пароля и Steam Guard.
/// </summary>
public sealed record SteamAuthState(
    string AccountName,
    string RefreshToken,
    string? GuardData);
