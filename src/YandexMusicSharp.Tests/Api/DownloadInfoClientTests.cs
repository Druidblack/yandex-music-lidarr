using System.Net;
using NUnit.Framework;
using YandexMusicSharp;
using YandexMusicSharp.Api;
using YandexMusicSharp.Auth;
using YandexMusicSharp.Http;
using YandexMusicSharp.Tests.Http;

namespace YandexMusicSharp.Tests.Api;

[TestFixture]
public class DownloadInfoClientTests
{
    private const string SampleResponse = """
        {
          "result": {
            "downloadInfo": {
              "trackId": "123",
              "quality": "lossless",
              "codec": "flac",
              "bitrate": 1411,
              "transport": "encraw",
              "size": 1000,
              "urls": ["https://cdn/x"],
              "key": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
            }
          }
        }
        """;

    [Test]
    public async Task GetAsync_BuildsSignedQuery_AndUnwrapsDownloadInfo()
    {
        // Frozen timestamp so the signature is deterministic across runs.
        const long timestamp = 1_700_000_000L;
        const long trackId = 123_456L;
        var expectedSignature = RequestSigner.SignDownloadInfo(
            timestamp,
            trackId,
            "lossless",
            DownloadInfoClient.Codecs,
            DownloadInfoClient.Transports);

        var stub = new StubHttpMessageHandler(SampleResponse);
        using var httpClient = new HttpClient(stub);
        using var http = new YandexMusicHttpClient(new YandexMusicCredentials("t"), httpClient);
        var sut = new DownloadInfoClient(http, () => timestamp);

        var info = await sut.GetAsync(trackId, YandexQuality.Lossless);

        Assert.That(info.Codec, Is.EqualTo("flac"));
        Assert.That(info.Key, Does.StartWith("0123456789abcdef"));

        var url = stub.Requests[0].RequestUri!.AbsoluteUri;
        Assert.That(url, Does.Contain("/get-file-info?"));
        Assert.That(url, Does.Contain($"ts={timestamp}"));
        Assert.That(url, Does.Contain($"trackId={trackId}"));
        Assert.That(url, Does.Contain("quality=lossless"));
        Assert.That(url, Does.Contain("transports=encraw"));
        Assert.That(url, Does.Contain($"sign={Uri.EscapeDataString(expectedSignature)}"));
    }

    [Test]
    public void GetAsync_Throws_WhenServerOmitsDownloadInfo()
    {
        var stub = new StubHttpMessageHandler("""{"result":{}}""", HttpStatusCode.OK);
        using var httpClient = new HttpClient(stub);
        using var http = new YandexMusicHttpClient(new YandexMusicCredentials("t"), httpClient);
        var sut = new DownloadInfoClient(http, () => 0);

        Assert.ThrowsAsync<YandexMusicException>(() => sut.GetAsync(1, YandexQuality.Low));
    }
}
