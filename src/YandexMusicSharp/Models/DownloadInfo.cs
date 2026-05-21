using System.Text.Json.Serialization;

namespace YandexMusicSharp.Models;

/// <summary>
/// Response payload of <c>GET /get-file-info?transports=encraw</c>.  The
/// nested <c>downloadInfo</c> object carries the encrypted-stream URLs and
/// the AES-256 key.
/// </summary>
public sealed class DownloadInfoEnvelope
{
    [JsonPropertyName("downloadInfo")]
    public DownloadInfo? DownloadInfo { get; init; }
}

public sealed class DownloadInfo
{
    [JsonPropertyName("trackId")]
    public string? TrackId { get; init; }

    [JsonPropertyName("quality")]
    public string Quality { get; init; } = string.Empty;

    [JsonPropertyName("codec")]
    public string Codec { get; init; } = string.Empty;

    [JsonPropertyName("bitrate")]
    public int Bitrate { get; init; }

    [JsonPropertyName("transport")]
    public string? Transport { get; init; }

    [JsonPropertyName("size")]
    public long? Size { get; init; }

    [JsonPropertyName("urls")]
    public IReadOnlyList<string> Urls { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 64-character hex string = 256-bit AES key.  Present whenever the
    /// transport is <c>encraw</c>.  Decrypted with
    /// <c>YandexMusicSharp.Codec.AesCtrDecryptor</c>.
    /// </summary>
    [JsonPropertyName("key")]
    public string? Key { get; init; }
}
