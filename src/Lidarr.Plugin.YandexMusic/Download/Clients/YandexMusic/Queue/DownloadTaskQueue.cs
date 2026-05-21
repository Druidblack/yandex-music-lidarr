using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Plugins.YandexMusic;
using YandexMusicSharp;
using YandexMusicSharp.Models;

namespace NzbDrone.Core.Download.Clients.YandexMusic.Queue
{
    /// <summary>
    /// In-process background processor for queued album downloads.  Uses a
    /// bounded <see cref="Channel{T}"/> for backpressure and a single consumer
    /// loop with a per-album SemaphoreSlim for track parallelism.
    /// </summary>
    public sealed class DownloadTaskQueue : IDisposable
    {
        private readonly Channel<DownloadItem> _channel;
        private readonly ConcurrentDictionary<string, DownloadItem> _items = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly Logger _logger;
        private YandexMusicSettings? _settings;
        private Task? _consumerLoop;

        public DownloadTaskQueue(int capacity, Logger logger)
        {
            _channel = Channel.CreateBounded<DownloadItem>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
            });
            _logger = logger;
        }

        public void StartQueueHandler()
        {
            _consumerLoop ??= Task.Run(() => RunConsumerLoop(_cts.Token));
        }

        public void SetSettings(YandexMusicSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public async ValueTask EnqueueAsync(DownloadItem item, CancellationToken cancellationToken = default)
        {
            _items[item.Id] = item;
            await _channel.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
        }

        public IEnumerable<DownloadItem> Snapshot() => _items.Values.ToArray();

        public bool TryGet(string id, out DownloadItem? item) => _items.TryGetValue(id, out item);

        public void Remove(string id)
        {
            _items.TryRemove(id, out _);
        }

        public void Dispose()
        {
            _channel.Writer.TryComplete();
            _cts.Cancel();
            _cts.Dispose();
        }

        private async Task RunConsumerLoop(CancellationToken cancellationToken)
        {
            try
            {
                while (await _channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (_channel.Reader.TryRead(out var item))
                    {
                        await ProcessAsync(item, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // shutdown
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "Yandex.Music download queue stopped unexpectedly.");
            }
        }

        private async Task ProcessAsync(DownloadItem item, CancellationToken cancellationToken)
        {
            if (_settings is null)
            {
                MarkFailed(item, "Download client settings were not initialised.");
                return;
            }

            try
            {
                item.Status = DownloadItemStatus.Downloading;

                var client = YandexMusicApi.GetClient(_settings.OAuthToken);
                var album = await client.Albums.GetWithTracksAsync(item.YandexAlbumId, cancellationToken).ConfigureAwait(false);
                if (album.Volumes is null || album.Volumes.Count == 0)
                {
                    MarkFailed(item, "Album has no tracks - it may be unavailable in the current region.");
                    return;
                }

                var albumFolder = BuildAlbumFolder(_settings.DownloadPath, album);
                Directory.CreateDirectory(albumFolder);
                item.OutputPath = new OsPath(albumFolder);

                var tracks = album.Volumes.SelectMany(volume => volume).Where(t => t.Available).ToList();
                if (tracks.Count == 0)
                {
                    MarkFailed(item, "No tracks are available for this album with the bound account.");
                    return;
                }

                using var concurrency = new SemaphoreSlim(_settings.MaxConcurrentTracks);
                var downloadTasks = new List<Task>(tracks.Count);

                foreach (var track in tracks)
                {
                    await concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
                    downloadTasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await DownloadTrackAsync(client, track, album, albumFolder, item, cancellationToken).ConfigureAwait(false);
                        }
                        finally
                        {
                            concurrency.Release();
                        }
                    }, cancellationToken));
                }

                await Task.WhenAll(downloadTasks).ConfigureAwait(false);

                if (item.Status != DownloadItemStatus.Failed)
                {
                    item.Status = DownloadItemStatus.Completed;
                    item.Message = "All tracks downloaded.";
                }
            }
            catch (OperationCanceledException)
            {
                item.Status = DownloadItemStatus.Failed;
                item.Message = "Cancelled.";
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "Failed to process Yandex.Music download {0}.", item.Id);
                MarkFailed(item, ex.Message);
            }
        }

        private async Task DownloadTrackAsync(
            YandexMusicClient client,
            Track track,
            Album album,
            string albumFolder,
            DownloadItem item,
            CancellationToken cancellationToken)
        {
            if (_settings is null)
            {
                return;
            }
            if (!long.TryParse(track.Id, out var trackId))
            {
                throw new FormatException($"Track id '{track.Id}' is not numeric.");
            }

            var info = await client.Downloads.GetAsync(trackId, item.Quality, cancellationToken).ConfigureAwait(false);
            var bytes = await client.DownloadDecryptedAsync(info, cancellationToken).ConfigureAwait(false);

            var fileName = TrackFileNamer.BuildFileName(track, album, info);
            var fullPath = Path.Combine(albumFolder, fileName);

            await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken).ConfigureAwait(false);

            try
            {
                MetadataUtilities.WriteTags(fullPath, track, album, item.RemoteAlbum);
            }
            catch (Exception ex)
            {
                _logger?.Warn(ex, "Failed to write tags to {0}; the file is still on disk.", fullPath);
            }

            item.DownloadedSize += info.Size ?? bytes.LongLength;

            if (_settings.DownloadDelayMs > 0)
            {
                await Task.Delay(_settings.DownloadDelayMs, cancellationToken).ConfigureAwait(false);
            }
        }

        private static string BuildAlbumFolder(string root, Album album)
        {
            var artist = album.Artists is { Count: > 0 } artists ? artists[0].Name : "Unknown Artist";
            return Path.Combine(root, PathSanitizer.Sanitize(artist), PathSanitizer.Sanitize(album.Title));
        }

        private static void MarkFailed(DownloadItem item, string message)
        {
            item.Status = DownloadItemStatus.Failed;
            item.Message = message;
        }
    }
}
