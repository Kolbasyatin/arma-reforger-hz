using ArmaReforger.Identity.Configuration;
using Microsoft.Extensions.Options;
using SteamKit2.Authentication;

namespace ArmaReforger.Identity.Steam;

/// <summary>
/// Полный вход по логину/паролю со Steam Guard. Нужен один раз:
/// результат — SteamAuthState с refresh token, дальше сервис входит по нему.
/// Откуда берётся код Steam Guard, решает переданный IAuthenticator.
/// </summary>
public sealed class SteamCredentialsLogin
{
    private readonly SteamOptions _options;
    private readonly ILogger<SteamCredentialsLogin> _logger;

    public SteamCredentialsLogin(
        IOptions<SteamOptions> options,
        ILogger<SteamCredentialsLogin> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SteamAuthState> LoginAsync(
        SteamCredentials credentials,
        IAuthenticator authenticator,
        string? previousGuardData,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await SteamConnection.ConnectAsync(
            _options.OperationTimeout,
            _logger,
            cancellationToken);

        _logger.LogInformation("Starting Steam auth session for {Username}", credentials.Username);

        var session = await connection.Authentication.BeginAuthSessionViaCredentialsAsync(
            new AuthSessionDetails
            {
                Username = credentials.Username,
                Password = credentials.Password,
                IsPersistentSession = true,
                GuardData = previousGuardData,
                Authenticator = authenticator
            });

        var result = await session.PollingWaitForResultAsync(cancellationToken);

        // Граница этапов: до сюда падает Steam Guard/пароль, после — проверочный вход по refresh token.
        _logger.LogInformation("Steam issued refresh token for {AccountName}", result.AccountName);

        var authState = new SteamAuthState(
            result.AccountName,
            result.RefreshToken,
            result.NewGuardData ?? previousGuardData);

        // Проверяем, что refresh token действительно пускает в аккаунт.
        await connection.LogOnAsync(authState, _options.OperationTimeout, cancellationToken);

        _logger.LogInformation("Steam credentials login succeeded for {AccountName}", authState.AccountName);

        return authState;
    }
}
