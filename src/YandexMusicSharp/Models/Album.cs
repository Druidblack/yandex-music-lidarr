using System.Text.Json.Serialization;

namespace YandexMusicSharp.Models;

public sealed class Album
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("year")]
    public int? Year { get; init; }

    [JsonPropertyName("releaseDate")]
    public string? ReleaseDate { get; init; }

    [JsonPropertyName("trackCount")]
    public int TrackCount { get; init; }

    [JsonPropertyName("genre")]
    public string? Genre { get; init; }

    [JsonPropertyName("metaType")]
    public string? MetaType { get; init; }

    [JsonPropertyName("coverUri")]
    public string? CoverUri { get; init; }

    [JsonPropertyName("available")]
    public bool? Available { get; init; }

    [JsonPropertyName("artists")]
    public IReadOnlyList<Artist>? Artists { get; init; }

    /// <summary>
    /// Present only on responses from <c>/albums/{id}/with-tracks</c>.  Outer list is
    /// the disc/volume index (1-based), inner list is the tracks of that disc.
    /// </summary>
    [JsonPropertyName("volumes")]
    public IReadOnlyList<IReadOnlyList<Track>>? Volumes { get; init; }
}
