using System.Net;

namespace YandexMusicSharp.Http;

/// <summary>
/// Thrown when <c>api.music.yandex.net</c> returns a non-success response or the body
/// does not match the expected envelope.
/// </summary>
public sealed class YandexMusicException : Exception
{
    public YandexMusicException(string message) : base(message)
    {
    }

    public YandexMusicException(string message, Exception inner) : base(message, inner)
    {
    }

    public HttpStatusCode? StatusCode { get; init; }

    public string? ResponseBody { get; init; }
}
