using System.Net;
using NUnit.Framework;
using YandexMusicSharp.Auth;
using YandexMusicSharp.Http;
using YandexMusicSharp.Models;

namespace YandexMusicSharp.Tests.Http;

[TestFixture]
public class YandexMusicHttpClientTests
{
    private static YandexMusicCredentials Credentials => new("test-oauth-token");

    [Test]
    public async Task GetAsync_SetsRequiredHeaders()
    {
        var stub = new StubHttpMessageHandler("""{"result":{"text":"hello"}}""");
        using var httpClient = new HttpClient(stub);
        using var sut = new YandexMusicHttpClient(Credentials, httpClient);

        _ = await sut.GetAsync<SearchResult>("/search", new Dictionary<string, string?> { ["text"] = "hello" });

        Assert.That(stub.Requests, Has.Count.EqualTo(1));
        var request = stub.Requests[0];
        Assert.That(request.Headers.Authorization?.Scheme, Is.EqualTo("OAuth"));
        Assert.That(request.Headers.Authorization?.Parameter, Is.EqualTo("test-oauth-token"));
        Assert.That(request.Headers.GetValues("X-Yandex-Music-Client"),
            Does.Contain("YandexMusicAndroid/24023621"));
        Assert.That(request.Headers.UserAgent.ToString(), Contains.Substring("Yandex-Music-API"));
    }

    [Test]
    public async Task GetAsync_AppendsQueryParameters_UrlEncoded()
    {
        var stub = new StubHttpMessageHandler("""{"result":{}}""");
        using var httpClient = new HttpClient(stub);
        using var sut = new YandexMusicHttpClient(Credentials, httpClient);

        _ = await sut.GetAsync<SearchResult>("/search", new Dictionary<string, string?>
        {
            ["text"] = "arctic monkeys",
            ["type"] = "album",
        });

        // Uri.ToString() displays a partially-unescaped form (spaces show as raw spaces),
        // but AbsoluteUri is what the HTTP client serialises on the wire.
        var url = stub.Requests[0].RequestUri!.AbsoluteUri;
        Assert.That(url, Does.Contain("/search?"));
        Assert.That(url, Does.Contain("text=arctic%20monkeys"));
        Assert.That(url, Does.Contain("type=album"));
    }

    [Test]
    public async Task GetAsync_UnwrapsResultEnvelope()
    {
        var json = """{"result":{"text":"foo","albums":{"total":1,"perPage":1,"order":0,"results":[{"id":42,"title":"x","trackCount":1}]}}}""";
        var stub = new StubHttpMessageHandler(json);
        using var httpClient = new HttpClient(stub);
        using var sut = new YandexMusicHttpClient(Credentials, httpClient);

        var result = await sut.GetAsync<SearchResult>("/search");

        Assert.That(result.Text, Is.EqualTo("foo"));
        Assert.That(result.Albums?.Results, Has.Count.EqualTo(1));
        Assert.That(result.Albums!.Results[0].Id, Is.EqualTo(42));
    }

    [Test]
    public void GetAsync_Throws_OnNonSuccessStatus()
    {
        var stub = new StubHttpMessageHandler("""{"error":{"name":"unauthorized"}}""", HttpStatusCode.Unauthorized);
        using var httpClient = new HttpClient(stub);
        using var sut = new YandexMusicHttpClient(Credentials, httpClient);

        var ex = Assert.ThrowsAsync<YandexMusicException>(
            () => sut.GetAsync<SearchResult>("/search"));
        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public void GetAsync_Throws_OnEmptyResult()
    {
        var stub = new StubHttpMessageHandler("""{"invocationInfo":{}}""");
        using var httpClient = new HttpClient(stub);
        using var sut = new YandexMusicHttpClient(Credentials, httpClient);

        Assert.ThrowsAsync<YandexMusicException>(() => sut.GetAsync<SearchResult>("/search"));
    }
}
