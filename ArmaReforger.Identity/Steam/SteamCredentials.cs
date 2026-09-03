namespace ArmaReforger.Identity.Steam;

/// <summary>Логин и пароль для первого, интерактивного входа.</summary>
public sealed record SteamCredentials(string Username, string Password);
