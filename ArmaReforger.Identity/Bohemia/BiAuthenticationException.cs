using System.Net;

namespace ArmaReforger.Identity.Bohemia;

/// <summary>Bohemia отклонила обмен Steam-билета на access token.</summary>
public sealed class BiAuthenticationException : Exception
{
    public BiAuthenticationException(HttpStatusCode statusCode, string responseBody)
        : base($"BI authentication failed: {(int)statusCode} {statusCode}")
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public HttpStatusCode StatusCode { get; }

    public string ResponseBody { get; }
}
