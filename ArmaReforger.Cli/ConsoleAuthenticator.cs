using SteamKit2.Authentication;

namespace ArmaReforger.Cli;

/// <summary>
/// Steam Guard через консоль: код из письма или приложения вводит человек.
/// </summary>
internal sealed class ConsoleAuthenticator : IAuthenticator
{
    public Task<string> GetDeviceCodeAsync(bool previousCodeWasIncorrect)
    {
        if (previousCodeWasIncorrect)
        {
            Console.WriteLine("Previous Steam Guard code was incorrect");
        }

        return Task.FromResult(ConsoleInput.Read("Steam Guard code: "));
    }

    public Task<string> GetEmailCodeAsync(string email, bool previousCodeWasIncorrect)
    {
        if (previousCodeWasIncorrect)
        {
            Console.WriteLine("Previous email code was incorrect");
        }

        return Task.FromResult(ConsoleInput.Read($"Code sent to {email}: "));
    }

    public Task<bool> AcceptDeviceConfirmationAsync()
    {
        Console.WriteLine("Confirm the login in the Steam mobile application");

        return Task.FromResult(true);
    }
}
