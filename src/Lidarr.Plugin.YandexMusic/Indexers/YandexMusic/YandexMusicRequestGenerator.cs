using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.IndexerSearch.Definitions;
using YandexMusicSharp.Http;

namespace NzbDrone.Core.Indexers.YandexMusic
{
    public class YandexMusicRequestGenerator : IIndexerRequestGenerator
    {
        private const int PageSize = 50;

        public YandexMusicIndexerSettings Settings { get; set; } = new();
        public Logger Logger { get; set; }

        public IndexerPageableRequestChain GetRecentRequests()
        {
            // Streaming services have no recent-releases feed; return a harmless dummy so
            // that Lidarr's "Test" button has something to call when the user saves.
            var chain = new IndexerPageableRequestChain();
            chain.Add(BuildSearch("never gonna give you up", searchType: "album"));
            return chain;
        }

        public IndexerPageableRequestChain GetSearchRequests(AlbumSearchCriteria searchCriteria)
        {
            var chain = new IndexerPageableRequestChain();
            var query = $"{searchCriteria.ArtistQuery} {searchCriteria.AlbumQuery}".Trim();
            chain.AddTier(BuildSearch(query, searchType: "album"));
            return chain;
        }

        public IndexerPageableRequestChain GetSearchRequests(ArtistSearchCriteria searchCriteria)
        {
            var chain = new IndexerPageableRequestChain();
            chain.AddTier(BuildSearch(searchCriteria.ArtistQuery, searchType: "artist"));
            return chain;
        }

        private IEnumerable<IndexerRequest> BuildSearch(string query, string searchType)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                yield break;
            }

            var baseUrl = string.IsNullOrWhiteSpace(Settings.BaseUrl)
                ? YandexMusicHttpClient.BaseUrl
                : Settings.BaseUrl.TrimEnd('/');

            var url = string.Concat(
                baseUrl,
                "/search?",
                "text=", Uri.EscapeDataString(query),
                "&nocorrect=False",
                "&type=", searchType,
                "&page=0",
                "&page-size=", PageSize.ToString(CultureInfo.InvariantCulture),
                "&playlist-in-best=True");

            var request = new HttpRequest(url, HttpAccept.Json)
            {
                Method = HttpMethod.Get,
            };
            request.Headers.Add("Authorization", $"OAuth {Settings.OAuthToken}");
            request.Headers.Add("X-Yandex-Music-Client", YandexMusicHttpClient.DefaultClientHeader);
            request.Headers["User-Agent"] = YandexMusicHttpClient.DefaultUserAgent;

            yield return new IndexerRequest(request);
        }
    }
}
