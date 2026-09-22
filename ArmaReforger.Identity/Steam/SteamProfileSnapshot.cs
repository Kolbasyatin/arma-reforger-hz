using System.Text.Json;

namespace ArmaReforger.Identity.Steam;

/// <summary>
/// Ответы Valve КАК ЕСТЬ, без разбора. Разбирать их здесь нечем и незачем: форма чужая,
/// меняется без предупреждения, и решать, что из неё нужно, будет потребитель.
/// </summary>
/// <param name="Source">Какой метод Valve: summary | friends | games | bans.</param>
/// <param name="Body">Тело ответа. null, если запрос не удался — см. Error.</param>
/// <param name="Error">Почему не удалось. Закрытый профиль — обычное дело, а не поломка.</param>
public sealed record SteamSource(string Source, JsonElement? Body, string? Error);

/// <param name="SteamId">SteamID64, как его прислал вызывающий.</param>
/// <param name="FetchedAt">Момент сбора. Проставляется здесь, а не потребителем: он один на все источники.</param>
public sealed record SteamProfileSnapshot(string SteamId, DateTimeOffset FetchedAt, IReadOnlyList<SteamSource> Sources);
