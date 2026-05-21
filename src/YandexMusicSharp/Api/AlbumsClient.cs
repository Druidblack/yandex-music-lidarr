using YandexMusicSharp.Http;
using YandexMusicSharp.Models;

namespace YandexMusicSharp.Api;

/// <summary>
/// Album endpoints.  Only <c>/albums/{id}/with-tracks</c> is exposed
/// because that is the single call the download client needs - it returns
/// the full album metadata plus the disc-grouped track list in one round
/// trip.
/// </summary>
public sealed class AlbumsClient
{
    private readonly YandexMusicHttpClient _http;

    public AlbumsClient(YandexMusicHttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }

    public Task<Album> GetWithTracksAsync(long albumId, CancellationToken cancellationToken = default)
    {
        return _http.GetAsync<Album>(
            $"/albums/{albumId.ToString(System.Globalization.CultureInfo.InvariantCulture)}/with-tracks",
            query: null,
            cancellationToken: cancellationToken);
    }
}
