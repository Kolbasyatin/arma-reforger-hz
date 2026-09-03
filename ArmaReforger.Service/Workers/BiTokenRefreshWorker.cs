using ArmaReforger.Identity.Bohemia;
using ArmaReforger.Service.Configuration;
using ArmaReforger.Identity.Steam;
using ArmaReforger.Service.Tokens;
using Microsoft.Extensions.Options;

namespace ArmaReforger.Service.Workers;

/// <summary>
/// Фоновый цикл: Steam билет -> BI access token -> хранилище.
/// Первая попытка на старте приложения, дальше по таймеру:
/// при успехе — незадолго до истечения токена, при ошибке — через RetryDelay.
/// </summary>
public sealed class BiTokenRefreshWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBiTokenStore _tokenStore;
    private readonly TokenRefreshOptions _options;
    private readonly ILogger<BiTokenRefreshWorker> _logger;

    public BiTokenRefreshWorker(
        IServiceScopeFactory scopeFactory,
        IBiTokenStore tokenStore,
        IOptions<TokenRefreshOptions> options,
        ILogger<BiTokenRefreshWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _tokenStore = tokenStore;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = await RefreshAsync(stoppingToken);

            _logger.LogInformation("Next BI token refresh in {Delay}", delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Штатная остановка приложения.
            }
        }
    }

    /// <summary>
    /// Одна попытка. Возвращает, через сколько делать следующую.
    /// </summary>
    private async Task<TimeSpan> RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Scope на одну попытку: HttpClient берётся свежий, а не захваченный навсегда.
            await using var scope = _scopeFactory.CreateAsyncScope();

            var ticketProvider = scope.ServiceProvider.GetRequiredService<ISteamTicketProvider>();
            var identityClient = scope.ServiceProvider.GetRequiredService<IBiIdentityClient>();

            await using var ticket = await ticketProvider.AcquireAsync(cancellationToken);

            var token = await identityClient.AuthenticateAsync(ticket.Base64, cancellationToken);

            await _tokenStore.SetAsync(token, cancellationToken);

            return DelayUntil(token.ExpiresAt);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return TimeSpan.Zero;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "BI token refresh failed, retry in {RetryDelay}",
                _options.RetryDelay);

            return _options.RetryDelay;
        }
    }

    private TimeSpan DelayUntil(DateTimeOffset expiresAt)
    {
        var delay = expiresAt - _options.RefreshLeadTime - DateTimeOffset.UtcNow;

        return delay > _options.RetryDelay ? delay : _options.RetryDelay;
    }
}
