// Интерактивный вход в Steam. Запускается один раз руками:
// логин, пароль, код Steam Guard -> steam-auth.json.
// Дальше ArmaReforger.Service входит по сохранённому refresh token сам.

using ArmaReforger.Cli;
using ArmaReforger.Identity.Configuration;
using ArmaReforger.Identity.Steam;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using var loggerFactory = LoggerFactory.Create(logging => logging.AddSimpleConsole());

// Тот же источник настроек, что у сервиса (Steam__AuthStateFilePath и т.д.),
// иначе CLI и сервис разойдутся по разным файлам.
var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();

var steamOptions = Options.Create(
    configuration.GetSection(SteamOptions.SectionName).Get<SteamOptions>() ?? new SteamOptions());

var authStateStore = new FileSteamAuthStateStore(
    steamOptions,
    loggerFactory.CreateLogger<FileSteamAuthStateStore>());

var login = new SteamCredentialsLogin(
    steamOptions,
    loggerFactory.CreateLogger<SteamCredentialsLogin>());

var previousState = await authStateStore.LoadAsync();

if (previousState is not null)
{
    Console.WriteLine($"Existing auth state found for {previousState.AccountName} at {authStateStore.FilePath}");

    if (!ConsoleInput.Read("Overwrite? [y/N]: ").Equals("y", StringComparison.OrdinalIgnoreCase))
    {
        return 0;
    }
}

var credentials = new SteamCredentials(
    ConsoleInput.Read("Steam login: "),
    ConsoleInput.ReadSecret("Steam password: "));

try
{
    var authState = await login.LoginAsync(
        credentials,
        new ConsoleAuthenticator(),
        previousState?.GuardData);

    await authStateStore.SaveAsync(authState);

    Console.WriteLine($"Done. Auth state saved to {authStateStore.FilePath}");

    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Login failed: {exception.Message}");

    return 1;
}
