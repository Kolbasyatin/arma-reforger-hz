//Фактически это просто сервис получения токена для передачи его уже в сервис мониторинга. 

using System.Net.Http.Json;

using System.Text.Json;
using SteamKit2;
using SteamKit2.Authentication;
using System.Buffers.Binary;
using System.Net.Http.Json;


var authDirectory = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "ArmaReforgerMonitor");

var authFile = Path.Combine(authDirectory, "steam-auth.json");

SteamAuthState? savedAuth = null;

if (File.Exists(authFile))
{
    var json = File.ReadAllText(authFile);
    savedAuth = JsonSerializer.Deserialize<SteamAuthState>(json);
}

const string username = "myusername";
const string password = "mypassword";

var steamClient = new SteamClient();
var callbackManager = new CallbackManager(steamClient);
var steamUser = steamClient.GetHandler<SteamUser>()!;
var steamAuthTicket = steamClient.GetHandler<SteamAuthTicket>()!;

var running = true;

callbackManager.Subscribe<SteamClient.ConnectedCallback>(async _ =>
{
    Console.WriteLine("Connected to Steam CM");

    if (savedAuth is not null)
    {
        Console.WriteLine("Logging on with saved refresh token");

        steamUser.LogOn(new SteamUser.LogOnDetails
        {
            Username = savedAuth.AccountName,
            AccessToken = savedAuth.RefreshToken,
            ShouldRememberPassword = true
        });

        return;
    }

    Console.Write("Steam login: ");
    //Логин пароль - переделать на переменные окружения.
    /*var username = Console.ReadLine()
                   ?? throw new InvalidOperationException("Login is empty");

    Console.Write("Steam password: ");
    var password = Console.ReadLine()
                   ?? throw new InvalidOperationException("Password is empty");*/

    try
    {
        var authSession =
            await steamClient.Authentication.BeginAuthSessionViaCredentialsAsync(
                new AuthSessionDetails
                {
                    Username = username,
                    Password = password,
                    IsPersistentSession = true,
                    Authenticator = new ConsoleAuthenticator()
                });

        var authResult = await authSession.PollingWaitForResultAsync();

        savedAuth = new SteamAuthState(
            authResult.AccountName,
            authResult.RefreshToken,
            authResult.NewGuardData);

        Directory.CreateDirectory(authDirectory);

        File.WriteAllText(
            authFile,
            JsonSerializer.Serialize(savedAuth));

        Console.WriteLine($"Authentication state saved to {authFile}");

        steamUser.LogOn(new SteamUser.LogOnDetails
        {
            Username = savedAuth.AccountName,
            AccessToken = savedAuth.RefreshToken,
            ShouldRememberPassword = true
        });
    }
    catch (Exception exception)
    {
        Console.WriteLine($"Authentication failed: {exception.Message}");
        steamClient.Disconnect();
    }
});

callbackManager.Subscribe<SteamUser.LoggedOnCallback>(async callback =>
{
    Console.WriteLine($"Logon result: {callback.Result}");

    if (callback.Result == EResult.OK)
    {
        Console.WriteLine($"Logged in as SteamID: {steamClient.SteamID}");
    }

    try
    {
        const uint appId = 1874880;
        const string identity = "";

        using var ticketInfo =
            await steamAuthTicket.GetAuthTicketForWebApi(
                appId,
                identity);

        var ticket = TrimWebApiTicket(ticketInfo.Ticket);
        var ticketBase64 = Convert.ToBase64String(ticket);

        Console.WriteLine(
            $"SteamKit buffer: {ticketInfo.Ticket.Length} bytes");

        Console.WriteLine(
            $"Actual ticket: {ticket.Length} bytes");

        using var httpClient = new HttpClient();

        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Arma Reforger/1.8.0.10 (Client; Windows)");

        var request = new
        {
            platform = "steam",
            token = ticketBase64,

            platformOpts = new
            {
                appId = appId.ToString()
            }
        };

        // 1. Отправляем Steam ticket в Bohemia.

        var response = await httpClient.PostAsJsonAsync(
            "https://api-ar-id.bistudio.com/game-identity/api/v1.1/identities/reforger/auth?include=profile",
            request);

        var responseBody = await response.Content.ReadAsStringAsync();

        Console.WriteLine(
            $"BI auth response: {(int)response.StatusCode} " +
            $"{response.StatusCode}");

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine(responseBody);
            return;
        }

// 2. Разбираем ответ Bohemia и достаём BI access token.

        var biAuth = JsonSerializer.Deserialize<BiAuthResponse>(
                         responseBody,
                         new JsonSerializerOptions
                         {
                             PropertyNameCaseInsensitive = true
                         })
                     ?? throw new InvalidOperationException(
                         "Invalid BI authentication response");

        Console.WriteLine(
            $"BI access token received. Expires: " +
            $"{DateTimeOffset.FromUnixTimeSeconds(biAuth.AccessTokenExp):O}");

// 3. Используем BI access token для создания игровой BI-сессии.

        var sessionRequest = new
        {
            accessToken = biAuth.AccessToken,
            clientVersion = "1.8.0",
            platformId = "ReforgerSteam",
            gameVersion = "1.8.0.10",

            // Временно повторяем значение из перехваченного запроса.
            platformUsername = "Zalex"
        };

        var sessionResponse = await httpClient.PostAsJsonAsync(
            "https://api-ar-game.bistudio.com/game-api/api/v1.0/session/login",
            sessionRequest);

        var sessionResponseBody =
            await sessionResponse.Content.ReadAsStringAsync();

        Console.WriteLine(
            $"BI session response: {(int)sessionResponse.StatusCode} " +
            $"{sessionResponse.StatusCode}");

        Console.WriteLine(sessionResponseBody);
    }
    catch (Exception exception)
    {
        Console.WriteLine(
            $"Ticket request failed: {exception}");
    }
    finally
    {
        steamClient.Disconnect();
    }
});

callbackManager.Subscribe<SteamClient.DisconnectedCallback>(_ =>
{
    Console.WriteLine("Disconnected from Steam CM");
    running = false;
});

Console.WriteLine("Connecting...");
steamClient.Connect();

while (running)
{
    callbackManager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
}

static byte[] TrimWebApiTicket(byte[] ticket)
{
    var gameConnectTokenLength =
        BinaryPrimitives.ReadInt32LittleEndian(
            ticket.AsSpan(0, 4));

    var sessionLengthOffset =
        4 + gameConnectTokenLength;

    var sessionLength =
        BinaryPrimitives.ReadInt32LittleEndian(
            ticket.AsSpan(sessionLengthOffset, 4));

    var ownershipLengthOffset =
        sessionLengthOffset + 4 + sessionLength;

    var ownershipLength =
        BinaryPrimitives.ReadInt32LittleEndian(
            ticket.AsSpan(ownershipLengthOffset, 4));

    var actualLength =
        ownershipLengthOffset + 4 + ownershipLength;

    return ticket[..actualLength];
}

sealed record BiAuthResponse(
    string IdentityId,
    string AccessToken,
    long AccessTokenExp
    );

sealed class ConsoleAuthenticator : IAuthenticator
{
    public Task<string> GetDeviceCodeAsync(bool previousCodeWasIncorrect)
    {
        if (previousCodeWasIncorrect)
        {
            Console.WriteLine("Previous Steam Guard code was incorrect");
        }

        Console.Write("Steam Guard code: ");

        return Task.FromResult(Console.ReadLine()!.Trim());
    }

    public Task<string> GetEmailCodeAsync(
        string email,
        bool previousCodeWasIncorrect)
    {
        if (previousCodeWasIncorrect)
        {
            Console.WriteLine("Previous email code was incorrect");
        }

        Console.Write($"Code sent to {email}: ");

        return Task.FromResult(Console.ReadLine()!.Trim());
    }

    public Task<bool> AcceptDeviceConfirmationAsync()
    {
        Console.WriteLine(
            "Confirm the login in the Steam mobile application");

        return Task.FromResult(true);
    }
}

sealed record SteamAuthState(
    string AccountName,
    string RefreshToken,
    string? GuardData
);

//Пример запроса с уже полученным токеном.

    /*
    const string lobbySearchUrl =
        "https://api-ar-game.bistudio.com/game-api/api/v1.0/lobby/rooms/search";

    var accessToken = "тутживеттокен";


    using var client = new HttpClient();

    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Arma Reforger/1.8.0.10 (Client; Windows)"
    );

    var requestBody = new
    {
        directJoinCode = "",
        hostAddress = "",
        order = "PlayerCount",
        scenarioId = "",
        includePing = 0,
        text = "",
        minPlayersPercent = 0,
        maxPlayersPercent = 100,
        minPlayersCount = 0,
        maxPlayersCount = 256,
        modded = false,
        ascendent = false,
        gameClientFilter = "AnyCompatible",
        accessToken,
        clientVersion = "1.8.0",
        platformId = "ReforgerSteam",
        gameClientType = "PLATFORM_PC",
        lightweight = true,
        from = 0,
        limit = 50,
        pingValues = Array.Empty<object>()
    };

    var response = await client.PostAsJsonAsync(
        lobbySearchUrl,
        requestBody
    );

    var body = await response.Content.ReadAsStringAsync();

    Console.WriteLine($"HTTP {(int)response.StatusCode} {response.StatusCode}");
    Console.WriteLine(body);
    */

