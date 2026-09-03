namespace ArmaReforger.Identity.Configuration;

/// <summary>
/// Настройки Steam-части. Заполняются из appsettings/переменных окружения,
/// значения по умолчанию — подтверждённые экспериментом.
/// </summary>
public sealed class SteamOptions
{
    public const string SectionName = "Steam";

    /// <summary>AppID Arma Reforger.</summary>
    public uint AppId { get; init; } = 1874880;

    /// <summary>Bohemia принимает билет с пустым identity.</summary>
    public string TicketIdentity { get; init; } = string.Empty;

    /// <summary>
    /// Путь к файлу с состоянием аутентификации.
    /// Пусто — LocalApplicationData/ArmaReforgerMonitor/steam-auth.json.
    /// </summary>
    public string? AuthStateFilePath { get; init; }

    /// <summary>Предел ожидания подключения и логина.</summary>
    public TimeSpan OperationTimeout { get; init; } = TimeSpan.FromSeconds(30);
}
