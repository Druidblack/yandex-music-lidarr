namespace NzbDrone.Core.Indexers
{
    /// <summary>
    /// Marker type binding the Yandex.Music indexer to the Yandex.Music
    /// download client.  Lidarr matches releases to download clients via
    /// <see cref="ReleaseInfo.DownloadProtocol"/> == <c>nameof(YandexMusicDownloadProtocol)</c>.
    /// </summary>
    public class YandexMusicDownloadProtocol : IDownloadProtocol
    {
    }
}
