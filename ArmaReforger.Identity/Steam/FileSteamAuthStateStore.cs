using System.Text.Json;
using ArmaReforger.Identity.Configuration;
using Microsoft.Extensions.Options;

namespace ArmaReforger.Identity.Steam;

/// <summary>
/// Состояние в JSON-файле. Запись атомарная, на Unix права ограничены до 0600.
/// </summary>
public sealed class FileSteamAuthStateStore : ISteamAuthStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _filePath;
    private readonly ILogger<FileSteamAuthStateStore> _logger;

    public FileSteamAuthStateStore(
        IOptions<SteamOptions> options,
        ILogger<FileSteamAuthStateStore> logger)
    {
        _filePath = ResolveFilePath(options.Value.AuthStateFilePath);
        _logger = logger;
    }

    public string FilePath => _filePath;

    public async ValueTask<SteamAuthState?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            // Для CLI это штатно (первый вход), для сервиса причину скажет воркер. Здесь только след.
            _logger.LogDebug("Steam auth state file not found at {FilePath}", _filePath);

            return null;
        }

        await using var stream = File.OpenRead(_filePath);

        return await JsonSerializer.DeserializeAsync<SteamAuthState>(
            stream,
            SerializerOptions,
            cancellationToken);
    }

    public async ValueTask SaveAsync(
        SteamAuthState state,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_filePath)!;

        Directory.CreateDirectory(directory);

        // Пишем рядом и подменяем одним движением, чтобы падение
        // посреди записи не оставило обрезанный файл.
        var temporaryPath = _filePath + ".tmp";

        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                state,
                SerializerOptions,
                cancellationToken);
        }

        RestrictToOwner(temporaryPath);

        File.Move(temporaryPath, _filePath, overwrite: true);

        _logger.LogInformation("Steam auth state saved to {FilePath}", _filePath);
    }

    private static string ResolveFilePath(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ArmaReforgerMonitor",
            "steam-auth.json");
    }

    private static void RestrictToOwner(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
