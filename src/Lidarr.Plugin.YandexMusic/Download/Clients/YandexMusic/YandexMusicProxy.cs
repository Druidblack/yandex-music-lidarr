using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Core.Download.Clients.YandexMusic.Queue;
using NzbDrone.Core.Parser.Model;
using YandexMusicSharp;

namespace NzbDrone.Core.Download.Clients.YandexMusic
{
    /// <summary>
    /// Single-instance bridge between Lidarr's download client API and the
    /// background <see cref="DownloadTaskQueue"/>.  Held as a static singleton
    /// because Lidarr instantiates the download client on demand.
    /// </summary>
    public sealed class YandexMusicProxy : IYandexMusicProxy
    {
        private static readonly Lazy<YandexMusicProxy> InstanceHolder = new(() => new YandexMusicProxy(LogManager.GetCurrentClassLogger()));

        public static YandexMusicProxy Instance => InstanceHolder.Value;

        private readonly DownloadTaskQueue _queue;

        internal YandexMusicProxy(Logger logger)
        {
            _queue = new DownloadTaskQueue(capacity: 500, logger);
            _queue.StartQueueHandler();
        }

        public async Task<string> DownloadAsync(RemoteAlbum remoteAlbum, YandexMusicSettings settings)
        {
            ArgumentNullException.ThrowIfNull(remoteAlbum);
            ArgumentNullException.ThrowIfNull(settings);

            _queue.SetSettings(settings);

            var (albumId, quality) = DownloadItem.ParseDownloadUrl(remoteAlbum.Release.DownloadUrl);
            var item = new DownloadItem(
                id: Guid.NewGuid().ToString("N"),
                remoteAlbum: remoteAlbum,
                yandexAlbumId: albumId,
                quality: AlignQuality(quality, settings));

            await _queue.EnqueueAsync(item).ConfigureAwait(false);
            return item.Id;
        }

        public IEnumerable<DownloadClientItem> GetQueue(YandexMusicSettings settings)
        {
            _queue.SetSettings(settings);
            return _queue.Snapshot().Select(item => item.ToDownloadClientItem()).ToList();
        }

        public void RemoveFromQueue(string downloadId)
        {
            _queue.Remove(downloadId);
        }

        private static YandexQuality AlignQuality(YandexQuality fromRelease, YandexMusicSettings settings)
        {
            // The indexer emits one ReleaseInfo per quality - if the user explicitly
            // chose a quality on the download client we honour it, otherwise we use the
            // quality embedded in the release URL.
            var configured = (YandexQuality)settings.Quality;
            return configured == fromRelease ? fromRelease : configured;
        }
    }
}
