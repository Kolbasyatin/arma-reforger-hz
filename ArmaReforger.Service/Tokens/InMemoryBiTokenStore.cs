using ArmaReforger.Identity.Bohemia;
namespace ArmaReforger.Service.Tokens;

/// <summary>
/// Хранит токен в поле процесса. Регистрируется синглтоном,
/// поэтому воркер и HTTP-обработчики видят одно и то же значение.
/// </summary>
public sealed class InMemoryBiTokenStore : IBiTokenStore
{
    private BiToken? _token;

    public ValueTask<BiToken?> GetAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Volatile.Read(ref _token));

    public ValueTask SetAsync(BiToken token, CancellationToken cancellationToken = default)
    {
        Volatile.Write(ref _token, token);

        return ValueTask.CompletedTask;
    }
}
