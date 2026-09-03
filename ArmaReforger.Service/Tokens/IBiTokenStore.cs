using ArmaReforger.Identity.Bohemia;
namespace ArmaReforger.Service.Tokens;

/// <summary>
/// Хранилище текущего BI access token.
/// Пишет фоновый Steam-воркер, читают HTTP-обработчики.
/// Реализация подменяема: память сейчас, файл или Redis потом.
/// </summary>
public interface IBiTokenStore
{
    ValueTask<BiToken?> GetAsync(CancellationToken cancellationToken = default);

    ValueTask SetAsync(BiToken token, CancellationToken cancellationToken = default);
}
