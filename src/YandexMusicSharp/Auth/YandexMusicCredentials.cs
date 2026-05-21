namespace YandexMusicSharp.Auth;

/// <summary>
/// Holds the long-lived OAuth bearer token used to authenticate against
/// <c>api.music.yandex.net</c>.  Acquired interactively via the implicit OAuth
/// flow against <c>oauth.yandex.ru</c> with the well-known public client
/// id <c>23cabbbdc6cd418abb4b39c32c41195d</c> - see ymd docs for the exact
/// procedure.
/// </summary>
public sealed record YandexMusicCredentials(string AccessToken)
{
    public string AccessToken { get; } =
        !string.IsNullOrWhiteSpace(AccessToken)
            ? AccessToken
            : throw new ArgumentException("Access token must not be empty.", nameof(AccessToken));
}
