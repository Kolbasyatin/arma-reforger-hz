using SteamKit2;

namespace ArmaReforger.Cli;

/// <summary>
/// Протокольный трейс SteamKit в stderr. Включается STEAMKIT_DEBUG=1 —
/// когда Steam отвечает «задание не выполнено» без причины, это единственный способ увидеть обмен.
/// </summary>
internal sealed class SteamKitDebugListener : IDebugListener
{
    public static void EnableIfRequested()
    {
        var value = Environment.GetEnvironmentVariable("STEAMKIT_DEBUG");

        if (value is not ("1" or "true"))
        {
            return;
        }

        DebugLog.AddListener(new SteamKitDebugListener());
        DebugLog.Enabled = true;
    }

    public void WriteLine(string category, string msg)
    {
        Console.Error.WriteLine($"[steamkit:{category}] {msg}");
    }
}
