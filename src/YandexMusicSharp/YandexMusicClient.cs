using YandexMusicSharp.Api;
using YandexMusicSharp.Auth;
using YandexMusicSharp.Codec;
using YandexMusicSharp.Http;
using YandexMusicSharp.Models;

namespace YandexMusicSharp;

/// <summary>
/// Façade over the Yandex.Music API surface needed by the Lidarr plugin.
/// Exposes the indexer-side endpoints (search, artist discography), the
/// download-side endpoints (album with tracks, get-file-info) and a helper
/// that downloads an encrypted stream and returns the decrypted bytes.
/// </summary>
public sealed class YandexMusicClient : IDisposable
{
    private readonly YandexMusicHttpClient _http;

    public YandexMusicClient(YandexMusicCredentials credentials)
        : this(credentials, new HttpClient())
    {
    }

    public YandexMusicClient(YandexMusicCredentials credentials, HttpClient httpClient)
    {
        _http = new YandexMusicHttpClient(credentials, httpClient);
        Search = new SearchClient(_http);
        Albums = new AlbumsClient(_http);
        Artists = new ArtistsClient(_http);
        Downloads = new DownloadInfoClient(_http);
    }

    public SearchClient Search { get; }

    public AlbumsClient Albums { get; }

    public ArtistsClient Artists { get; }

    public DownloadInfoClient Downloads { get; }

    /// <summary>
    /// Fetches the first available CDN URL listed in <paramref name="info"/>,
    /// decrypts the bytes with the AES-256-CTR key carried in
    /// <see cref="DownloadInfo.Key"/>, and returns the plain audio file.
    /// Falls back to the next URL on transport errors.
    /// </summary>
    public async Task<byte[]> DownloadDecryptedAsync(DownloadInfo info, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (info.Urls.Count == 0)
        {
            throw new YandexMusicException("DownloadInfo carries no CDN URLs.");
        }
        if (string.IsNullOrEmpty(info.Key))
        {
            throw new YandexMusicException("DownloadInfo is missing an AES decryption key - was the encraw transport requested?");
        }

        Exception? lastError = null;
        foreach (var url in info.Urls)
        {
            try
            {
                var encrypted = await _http.GetBytesAsync(new Uri(url, UriKind.Absolute), cancellationToken).ConfigureAwait(false);
                return AesCtrDecryptor.Decrypt(info.Key, encrypted);
            }
            catch (Exception ex) when (ex is HttpRequestException or YandexMusicException)
            {
                lastError = ex;
            }
        }

        throw new YandexMusicException("All CDN URLs failed to deliver the encrypted track.", lastError!);
    }

    public void Dispose()
    {
        _http.Dispose();
    }
}
