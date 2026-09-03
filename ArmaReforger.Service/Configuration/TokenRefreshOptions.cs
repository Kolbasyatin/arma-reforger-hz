namespace ArmaReforger.Service.Configuration;

/// <summary>
/// Расписание обновления BI access token.
/// </summary>
public sealed class TokenRefreshOptions
{
    public const string SectionName = "TokenRefresh";

    /// <summary>За сколько до истечения токена идти за новым.</summary>
    public TimeSpan RefreshLeadTime { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Пауза после неудачной попытки.</summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMinutes(1);
}
