using System;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Indexers.YandexMusic
{
    /// <summary>
    /// Yandex.Music indexer.  Translates Lidarr's artist/album search criteria
    /// into <c>/search?type=album|artist</c> calls and surfaces each Yandex
    /// album as one <see cref="Parser.Model.ReleaseInfo"/> per quality tier.
    /// </summary>
    public class YandexMusicIndexer : HttpIndexerBase<YandexMusicIndexerSettings>
    {
        public override string Name => "Yandex.Music";
        public override string Protocol => nameof(YandexMusicDownloadProtocol);
        public override bool SupportsRss => false;
        public override bool SupportsSearch => true;
        public override int PageSize => 50;
        public override TimeSpan RateLimit => TimeSpan.FromSeconds(1);

        public YandexMusicIndexer(
            IHttpClient httpClient,
            IIndexerStatusService indexerStatusService,
            IConfigService configService,
            IParsingService parsingService,
            Logger logger)
            : base(httpClient, indexerStatusService, configService, parsingService, logger)
        {
        }

        public override IIndexerRequestGenerator GetRequestGenerator()
        {
            return new YandexMusicRequestGenerator
            {
                Settings = Settings,
                Logger = _logger,
            };
        }

        public override IParseIndexerResponse GetParser()
        {
            return new YandexMusicParser
            {
                Settings = Settings,
            };
        }
    }
}
