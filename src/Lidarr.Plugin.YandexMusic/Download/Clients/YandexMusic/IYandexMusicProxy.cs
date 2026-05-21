using System.Collections.Generic;
using System.Threading.Tasks;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Download.Clients.YandexMusic
{
    public interface IYandexMusicProxy
    {
        Task<string> DownloadAsync(RemoteAlbum remoteAlbum, YandexMusicSettings settings);

        IEnumerable<DownloadClientItem> GetQueue(YandexMusicSettings settings);

        void RemoveFromQueue(string downloadId);
    }
}
