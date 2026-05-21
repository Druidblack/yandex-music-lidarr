using System;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Parser.Model;
using YandexMusicSharp;

namespace NzbDrone.Core.Download.Clients.YandexMusic.Queue
{
    /// <summary>
    /// One queued album download.  Lives in the in-process
    /// <see cref="DownloadTaskQueue"/> until it finishes (success or failure),
    /// at which point Lidarr's importer picks the files up off disk.
    /// </summary>
    public sealed class DownloadItem
    {
        public DownloadItem(string id, RemoteAlbum remoteAlbum, long yandexAlbumId, YandexQuality quality)
        {
            Id = id;
            RemoteAlbum = remoteAlbum ?? throw new ArgumentNullException(nameof(remoteAlbum));
            YandexAlbumId = yandexAlbumId;
            Quality = quality;
            Status = DownloadItemStatus.Queued;
            QueuedAt = DateTime.UtcNow;
        }

        public string Id { get; }

        public RemoteAlbum RemoteAlbum { get; }

        public long YandexAlbumId { get; }

        public YandexQuality Quality { get; }

        public DateTime QueuedAt { get; }

        public DownloadItemStatus Status { get; set; }

        public long TotalSize { get; set; }

        public long DownloadedSize { get; set; }

        public string Message { get; set; } = string.Empty;

        public OsPath OutputPath { get; set; }

        public string Title => RemoteAlbum.Release.Title;

        public DownloadClientItem ToDownloadClientItem()
        {
            var totalSize = TotalSize > 0 ? TotalSize : RemoteAlbum.Release.Size;
            var remaining = Math.Max(0, totalSize - DownloadedSize);
            return new DownloadClientItem
            {
                DownloadId = Id,
                Title = Title,
                TotalSize = totalSize,
                RemainingSize = remaining,
                OutputPath = OutputPath,
                Status = Status,
                Message = Message,
                CanBeRemoved = Status is DownloadItemStatus.Completed or DownloadItemStatus.Failed,
                CanMoveFiles = Status is DownloadItemStatus.Completed,
            };
        }

        /// <summary>
        /// Parses the custom <c>yandexmusic://album/{albumId}/quality/{quality}</c>
        /// URL emitted by the indexer back into structured fields.  The parsing
        /// itself lives in <see cref="DownloadAddress"/> over in YandexMusicSharp
        /// so it can be unit-tested without Lidarr.Core in the way.
        /// </summary>
        public static (long AlbumId, YandexQuality Quality) ParseDownloadUrl(string downloadUrl)
        {
            var address = DownloadAddress.Parse(downloadUrl);
            return (address.AlbumId, address.Quality);
        }
    }
}
