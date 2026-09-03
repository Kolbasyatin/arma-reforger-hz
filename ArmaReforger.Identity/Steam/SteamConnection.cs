using SteamKit2;

namespace ArmaReforger.Identity.Steam;

/// <summary>
/// Одно живое соединение со Steam CM и насос callback'ов SteamKit.
/// Освобождение закрывает соединение.
/// </summary>
public sealed class SteamConnection : IAsyncDisposable
{
    private static readonly TimeSpan PumpInterval = TimeSpan.FromMilliseconds(200);

    private readonly SteamClient _client;
    private readonly CallbackManager _callbackManager;
    private readonly CancellationTokenSource _pumpCancellation = new();
    private readonly List<IDisposable> _subscriptions = [];
    private readonly ILogger _logger;

    private readonly TaskCompletionSource _connected =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private TaskCompletionSource<EResult>? _loggedOn;
    private Task? _pump;
    private volatile bool _closing;

    private SteamConnection(ILogger logger)
    {
        _logger = logger;
        _client = new SteamClient();
        _callbackManager = new CallbackManager(_client);

        _subscriptions.Add(
            _callbackManager.Subscribe<SteamClient.ConnectedCallback>(
                _ => _connected.TrySetResult()));

        _subscriptions.Add(
            _callbackManager.Subscribe<SteamUser.LoggedOnCallback>(
                callback => _loggedOn?.TrySetResult(callback.Result)));

        _subscriptions.Add(
            _callbackManager.Subscribe<SteamClient.DisconnectedCallback>(
                _ => OnDisconnected()));
    }

    /// <summary>Аутентификация SteamKit: вход по логину/паролю и Steam Guard.</summary>
    public SteamKit2.Authentication.SteamAuthentication Authentication => _client.Authentication;

    public SteamAuthTicket AuthTicket => _client.GetHandler<SteamAuthTicket>()!;

    public SteamID? SteamId => _client.SteamID;

    /// <summary>Подключается к Steam CM. Аккаунт ещё не выбран.</summary>
    public static async Task<SteamConnection> ConnectAsync(
        TimeSpan timeout,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var connection = new SteamConnection(logger);

        try
        {
            connection.StartPump();

            connection._client.Connect();

            await connection._connected.Task.WaitAsync(timeout, cancellationToken);

            logger.LogInformation("Connected to Steam CM");

            return connection;
        }
        catch
        {
            await connection.DisposeAsync();

            throw;
        }
    }

    /// <summary>Входит в аккаунт по сохранённому refresh token.</summary>
    public async Task LogOnAsync(
        SteamAuthState authState,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        _loggedOn = new TaskCompletionSource<EResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        _client.GetHandler<SteamUser>()!.LogOn(
            new SteamUser.LogOnDetails
            {
                Username = authState.AccountName,
                AccessToken = authState.RefreshToken,
                ShouldRememberPassword = true
            });

        var result = await _loggedOn.Task.WaitAsync(timeout, cancellationToken);

        if (result != EResult.OK)
        {
            throw new SteamSessionException($"Steam logon failed: {result}");
        }

        _logger.LogInformation("Logged on to Steam as {SteamId}", _client.SteamID);
    }

    public async ValueTask DisposeAsync()
    {
        _closing = true;

        // Именно Disconnect, а не LogOff: LogOff способен аннулировать
        // persistent refresh token.
        _client.Disconnect();

        await _pumpCancellation.CancelAsync();

        if (_pump is not null)
        {
            await _pump;
        }

        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }

        _pumpCancellation.Dispose();
    }

    private void StartPump()
    {
        _pump = Task.Factory.StartNew(
            PumpCallbacks,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    private void PumpCallbacks()
    {
        while (!_pumpCancellation.IsCancellationRequested)
        {
            _callbackManager.RunWaitCallbacks(PumpInterval);
        }
    }

    private void OnDisconnected()
    {
        if (_closing)
        {
            _logger.LogInformation("Disconnected from Steam CM");

            return;
        }

        var error = new SteamSessionException("Disconnected from Steam CM unexpectedly");

        _connected.TrySetException(error);
        _loggedOn?.TrySetException(error);
    }
}
