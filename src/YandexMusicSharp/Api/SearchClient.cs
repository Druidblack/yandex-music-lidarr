using YandexMusicSharp.Http;
using YandexMusicSharp.Models;

namespace YandexMusicSharp.Api;

/// <summary>
/// Thin wrapper around <c>GET /search</c>.
/// </summary>
public sealed class SearchClient
{
    private readonly YandexMusicHttpClient _http;

    public SearchClient(YandexMusicHttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }

    public Task<SearchResult> SearchAsync(string query, SearchType type = SearchType.All, int page = 0, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Search query must not be empty.", nameof(query));
        }

        var parameters = new Dictionary<string, string?>
        {
            ["text"] = query,
            ["nocorrect"] = "False",
            ["type"] = type.ToApiString(),
            ["page"] = page.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["playlist-in-best"] = "True",
        };

        return _http.GetAsync<SearchResult>("/search", parameters, cancellationToken);
    }
}

public enum SearchType
{
    All,
    Artist,
    Album,
    Track,
    Playlist,
}

internal static class SearchTypeExtensions
{
    public static string ToApiString(this SearchType type) => type switch
    {
        SearchType.All => "all",
        SearchType.Artist => "artist",
        SearchType.Album => "album",
        SearchType.Track => "track",
        SearchType.Playlist => "playlist",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown search type."),
    };
}
