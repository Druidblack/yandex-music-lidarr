using System.Collections.Generic;
using System.Threading.Tasks;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.RemotePathMappings;

namespace NzbDrone.Core.Download.Clients.YandexMusic
{
    /// <summary>
    /// The Yandex.Music download client.  Forwards everything to a process-wide
    /// background queue (<see cref="YandexMusicProxy"/>) so <c>Download</c>
    /// returns quickly while the actual track downloading happens off-thread.
    /// </summary>
    public class YandexMusic : DownloadClientBase<YandexMusicSettings>
    {
        private readonly IYandexMusicProxy _proxy;

        public YandexMusic(
            IConfigService configService,
            IDiskProvider diskProvider,
            IRemotePathMappingService remotePathMappingService,
            ILocalizationService localizationService,
            Logger logger)
            : this(YandexMusicProxy.Instance, configService, diskProvider, remotePathMappingService, localizationService, logger)
        {
        }

        internal YandexMusic(
            IYandexMusicProxy proxy,
            IConfigService configService,
            IDiskProvider diskProvider,
            IRemotePathMappingService remotePathMappingService,
            ILocalizationService localizationService,
            Logger logger)
            : base(configService, diskProvider, remotePathMappingService, localizationService, logger)
        {
            _proxy = proxy;
        }

        public override string Name => "Yandex.Music";

        public override string Protocol => nameof(YandexMusicDownloadProtocol);

        public override async Task<string> Download(RemoteAlbum remoteAlbum, IIndexer indexer)
        {
            return await _proxy.DownloadAsync(remoteAlbum, Settings).ConfigureAwait(false);
        }

        public override IEnumerable<DownloadClientItem> GetItems()
        {
            foreach (var item in _proxy.GetQueue(Settings))
            {
                item.DownloadClientInfo = DownloadClientItemClientInfo.FromDownloadClient(this, hasPostImportCategory: false);
                yield return item;
            }
        }

        public override void RemoveItem(DownloadClientItem item, bool deleteData)
        {
            if (deleteData)
            {
                DeleteItemData(item);
            }
            _proxy.RemoveFromQueue(item.DownloadId);
        }

        public override DownloadClientInfo GetStatus()
        {
            return new DownloadClientInfo
            {
                IsLocalhost = true,
                OutputRootFolders = new List<OsPath> { new(Settings.DownloadPath) },
            };
        }

        protected override void Test(List<ValidationFailure> failures)
        {
            var folderFailure = TestFolder(Settings.DownloadPath, nameof(Settings.DownloadPath));
            if (folderFailure is not null)
            {
                failures.Add(folderFailure);
            }
        }
    }
}
