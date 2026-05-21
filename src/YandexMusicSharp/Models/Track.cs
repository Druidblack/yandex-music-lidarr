using System.Text.Json.Serialization;

namespace YandexMusicSharp.Models;

public sealed class Track
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("realId")]
    public string? RealId { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("durationMs")]
    public long DurationMs { get; init; }

    [JsonPropertyName("available")]
    public bool Available { get; init; }

    [JsonPropertyName("availableForPremiumUsers")]
    public bool AvailableForPremiumUsers { get; init; }

    [JsonPropertyName("coverUri")]
    public string? CoverUri { get; init; }

    [JsonPropertyName("artists")]
    public IReadOnlyList<Artist>? Artists { get; init; }

    [JsonPropertyName("albums")]
    public IReadOnlyList<TrackAlbumRef>? Albums { get; init; }

    [JsonPropertyName("lyricsInfo")]
    public TrackLyricsInfo? LyricsInfo { get; init; }
}

/// <summary>
/// Mirror of <see cref="Album"/> as it appears nested inside a track payload,
/// extended with the disc/track position metadata.
/// </summary>
public sealed class TrackAlbumRef
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("year")]
    public int? Year { get; init; }

    [JsonPropertyName("trackCount")]
    public int TrackCount { get; init; }

    [JsonPropertyName("genre")]
    public string? Genre { get; init; }

    [JsonPropertyName("metaType")]
    public string? MetaType { get; init; }

    [JsonPropertyName("coverUri")]
    public string? CoverUri { get; init; }

    [JsonPropertyName("artists")]
    public IReadOnlyList<Artist>? Artists { get; init; }

    [JsonPropertyName("trackPosition")]
    public TrackPosition? TrackPosition { get; init; }
}

public sealed class TrackPosition
{
    [JsonPropertyName("volume")]
    public int Volume { get; init; }

    [JsonPropertyName("index")]
    public int Index { get; init; }
}

public sealed class TrackLyricsInfo
{
    [JsonPropertyName("hasAvailableSyncLyrics")]
    public bool HasAvailableSyncLyrics { get; init; }

    [JsonPropertyName("hasAvailableTextLyrics")]
    public bool HasAvailableTextLyrics { get; init; }
}
