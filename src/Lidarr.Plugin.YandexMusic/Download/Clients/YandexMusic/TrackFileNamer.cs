using System.Globalization;
using YandexMusicSharp.Models;

namespace NzbDrone.Core.Download.Clients.YandexMusic
{
    internal static class TrackFileNamer
    {
        public static string BuildFileName(Track track, Album album, DownloadInfo info)
        {
            var trackPosition = ResolveTrackPosition(track, album);
            var paddedIndex = trackPosition.ToString("00", CultureInfo.InvariantCulture);
            var extension = ResolveExtension(info.Codec);
            var title = PathSanitizer.Sanitize(ComposeTitle(track.Title, track.Version));

            return $"{paddedIndex} - {title}.{extension}";
        }

        private static string ComposeTitle(string title, string? version)
            => string.IsNullOrWhiteSpace(version) ? title : $"{title} ({version})";

        private static int ResolveTrackPosition(Track track, Album album)
        {
            if (track.Albums is { Count: > 0 } albums)
            {
                foreach (var albumRef in albums)
                {
                    if (albumRef.Id == album.Id && albumRef.TrackPosition is { } pos)
                    {
                        return pos.Index;
                    }
                }
            }
            return 0;
        }

        private static string ResolveExtension(string codec) => codec?.ToLowerInvariant() switch
        {
            "flac" => "flac",
            "mp3" => "mp3",
            "aac" or "he-aac" or "aac-mp4" or "he-aac-mp4" or "flac-mp4" => "m4a",
            _ => "audio",
        };
    }
}
