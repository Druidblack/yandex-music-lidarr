using System.Text.Json.Serialization;

namespace YandexMusicSharp.Models;

public sealed class Artist
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("various")]
    public bool Various { get; init; }

    [JsonPropertyName("composer")]
    public bool Composer { get; init; }

    [JsonPropertyName("cover")]
    public Cover? Cover { get; init; }

    [JsonPropertyName("genres")]
    public IReadOnlyList<string>? Genres { get; init; }

    [JsonPropertyName("counts")]
    public ArtistCounts? Counts { get; init; }
}

public sealed class ArtistCounts
{
    [JsonPropertyName("tracks")]
    public int Tracks { get; init; }

    [JsonPropertyName("directAlbums")]
    public int DirectAlbums { get; init; }

    [JsonPropertyName("alsoAlbums")]
    public int AlsoAlbums { get; init; }
}

public sealed class Cover
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("uri")]
    public string? Uri { get; init; }
}
