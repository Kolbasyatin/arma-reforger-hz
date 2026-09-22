using System.Net;

namespace ArmaReforger.Identity.Bohemia;

/// <summary>Bohemia отклонила обмен Steam-билета на access token.</summary>
public sealed class BiAuthenticationException : Exception
{
    /// <summary>
    /// BI отвечает разными 404: маршрутным (Spring, поле "path") и смысловым (поле "apiCode").
    /// По одному коду их не различить, поэтому тело ответа идёт прямо в сообщение —
    /// логгер печатает Message, а до свойств исключения не добирается.
    /// </summary>
    public BiAuthenticationException(HttpStatusCode statusCode, string responseBody)
        : base(BuildMessage(statusCode, responseBody))
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public HttpStatusCode StatusCode { get; }

    public string ResponseBody { get; }

    private static string BuildMessage(HttpStatusCode statusCode, string responseBody)
    {
        var header = $"BI authentication failed: {(int)statusCode} {statusCode}";

        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return header;
        }

        // На ошибках инфраструктуры (прокси, WAF) вместо JSON приходит HTML-страница целиком.
        // Её в лог пускать незачем: хватает начала, чтобы понять, что это не BI.
        const int limit = 512;

        var body = responseBody.Trim();

        if (body.Length > limit)
        {
            body = string.Concat(body.AsSpan(0, limit), "… (truncated)");
        }

        return $"{header}. BI response: {body}";
    }
}
