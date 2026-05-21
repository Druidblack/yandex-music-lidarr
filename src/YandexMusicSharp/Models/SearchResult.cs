using System.Text.Json.Serialization;

namespace YandexMusicSharp.Models;

/// <summary>
/// Response payload of <c>GET /search</c>.  Each section is optional - omit
/// the corresponding type to skip parsing that block.
/// </summary>
public sealed class SearchResult
{
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("artists")]
    public SearchSection<Artist>? Artists { get; init; }

    [JsonPropertyName("albums")]
    public SearchSection<Album>? Albums { get; init; }

    [JsonPropertyName("tracks")]
    public SearchSection<Track>? Tracks { get; init; }
}

public sealed class SearchSection<T>
{
    [JsonPropertyName("total")]
    public int Total { get; init; }

    [JsonPropertyName("perPage")]
    public int PerPage { get; init; }

    [JsonPropertyName("order")]
    public int Order { get; init; }

    [JsonPropertyName("results")]
    public IReadOnlyList<T> Results { get; init; } = Array.Empty<T>();
}

public sealed class ArtistAlbumsPage
{
    [JsonPropertyName("albums")]
    public IReadOnlyList<Album> Albums { get; init; } = Array.Empty<Album>();

    [JsonPropertyName("pager")]
    public Pager? Pager { get; init; }
}
