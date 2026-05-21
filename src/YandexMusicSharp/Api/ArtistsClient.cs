using System.Globalization;
using YandexMusicSharp.Http;
using YandexMusicSharp.Models;

namespace YandexMusicSharp.Api;

/// <summary>
/// Artist endpoints.  Only the paginated <c>/artists/{id}/direct-albums</c>
/// endpoint is wrapped because it is the one the RSS / recent-releases
/// indexer path uses to enumerate an artist's discography.
/// </summary>
public sealed class ArtistsClient
{
    private readonly YandexMusicHttpClient _http;

    public ArtistsClient(YandexMusicHttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }

    public Task<ArtistAlbumsPage> GetDirectAlbumsAsync(
        long artistId,
        int page = 0,
        int pageSize = 20,
        ArtistAlbumSort sortBy = ArtistAlbumSort.Year,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["page"] = page.ToString(CultureInfo.InvariantCulture),
            ["page-size"] = pageSize.ToString(CultureInfo.InvariantCulture),
            ["sort-by"] = sortBy switch
            {
                ArtistAlbumSort.Year => "year",
                ArtistAlbumSort.Rating => "rating",
                _ => "year",
            },
        };

        return _http.GetAsync<ArtistAlbumsPage>(
            $"/artists/{artistId.ToString(CultureInfo.InvariantCulture)}/direct-albums",
            parameters,
            cancellationToken);
    }
}

public enum ArtistAlbumSort
{
    Year,
    Rating,
}
