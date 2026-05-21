using System.Globalization;
using YandexMusicSharp.Auth;
using YandexMusicSharp.Http;
using YandexMusicSharp.Models;

namespace YandexMusicSharp.Api;

/// <summary>
/// Wraps the non-public <c>GET /get-file-info</c> endpoint.  Builds the HMAC
/// signature, asks the server for an <c>encraw</c> transport, and returns
/// the resulting CDN URLs plus the AES decryption key.
///
/// The codec list is fixed to the seven formats the Android client accepts;
/// the server picks the best one available for the requested quality.
/// </summary>
public sealed class DownloadInfoClient
{
    public const string Codecs = "flac,flac-mp4,mp3,aac,he-aac,aac-mp4,he-aac-mp4";
    public const string Transports = "encraw";

    private readonly YandexMusicHttpClient _http;
    private readonly Func<long> _timestampSource;

    public DownloadInfoClient(YandexMusicHttpClient http)
        : this(http, () => DateTimeOffset.UtcNow.ToUnixTimeSeconds())
    {
    }

    internal DownloadInfoClient(YandexMusicHttpClient http, Func<long> timestampSource)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(timestampSource);
        _http = http;
        _timestampSource = timestampSource;
    }

    public async Task<DownloadInfo> GetAsync(
        long trackId,
        YandexQuality quality,
        CancellationToken cancellationToken = default)
    {
        var timestamp = _timestampSource();
        var qualityString = quality.ToApiString();
        var signature = RequestSigner.SignDownloadInfo(
            timestamp,
            trackId,
            qualityString,
            Codecs,
            Transports);

        var parameters = new Dictionary<string, string?>
        {
            ["ts"] = timestamp.ToString(CultureInfo.InvariantCulture),
            ["trackId"] = trackId.ToString(CultureInfo.InvariantCulture),
            ["quality"] = qualityString,
            ["codecs"] = Codecs,
            ["transports"] = Transports,
            ["sign"] = signature,
        };

        var envelope = await _http.GetAsync<DownloadInfoEnvelope>(
            "/get-file-info",
            parameters,
            cancellationToken).ConfigureAwait(false);

        if (envelope.DownloadInfo is null)
        {
            throw new YandexMusicException("get-file-info response did not include a downloadInfo object.");
        }

        return envelope.DownloadInfo;
    }
}
