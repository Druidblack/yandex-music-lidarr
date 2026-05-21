using System.Globalization;

namespace YandexMusicSharp;

/// <summary>
/// Parses the custom <c>yandexmusic://album/{albumId}/quality/{quality}</c>
/// URL scheme the indexer emits and the download client consumes.
///
/// Living in YandexMusicSharp rather than the plugin assembly means the parsing
/// logic can be unit-tested without dragging in Lidarr.Core.
/// </summary>
public readonly record struct DownloadAddress(long AlbumId, YandexQuality Quality)
{
    public const string Scheme = "yandexmusic";
    public const string Prefix = "yandexmusic://album/";

    public static DownloadAddress Parse(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("Download URL must not be empty.", nameof(url));
        }

        if (!url.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException($"Download URL '{url}' does not start with '{Prefix}'.");
        }

        var remainder = url[Prefix.Length..];
        var parts = remainder.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 || !string.Equals(parts[1], "quality", StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException($"Download URL '{url}' is missing the '/quality/<tier>' suffix.");
        }

        if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var albumId))
        {
            throw new FormatException($"Album id '{parts[0]}' in download URL is not a number.");
        }

        if (!Enum.TryParse<YandexQuality>(parts[2], ignoreCase: true, out var quality))
        {
            throw new FormatException($"Quality '{parts[2]}' in download URL is not a known YandexQuality value.");
        }

        return new DownloadAddress(albumId, quality);
    }

    public override string ToString()
        => $"{Prefix}{AlbumId.ToString(CultureInfo.InvariantCulture)}/quality/{Quality.ToString().ToLowerInvariant()}";
}
