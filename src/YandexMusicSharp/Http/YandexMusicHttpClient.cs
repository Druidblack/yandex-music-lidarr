using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using YandexMusicSharp.Auth;
using YandexMusicSharp.Models;

namespace YandexMusicSharp.Http;

/// <summary>
/// HTTP transport for <c>api.music.yandex.net</c>.  Adds the bearer token
/// and the X-Yandex-Music-Client header that unlocks lossless on every
/// request, unwraps the standard <c>{ invocationInfo, result }</c> envelope
/// and surfaces non-success responses as <see cref="YandexMusicException"/>.
/// </summary>
public sealed class YandexMusicHttpClient : IDisposable
{
    public const string BaseUrl = "https://api.music.yandex.net";
    public const string DefaultUserAgent = "Yandex-Music-API";
    public const string DefaultClientHeader = "YandexMusicAndroid/24023621";

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _client;
    private readonly bool _ownsClient;

    public YandexMusicHttpClient(YandexMusicCredentials credentials)
        : this(credentials, new HttpClient(), ownsClient: true)
    {
    }

    public YandexMusicHttpClient(YandexMusicCredentials credentials, HttpClient client)
        : this(credentials, client, ownsClient: false)
    {
    }

    private YandexMusicHttpClient(YandexMusicCredentials credentials, HttpClient client, bool ownsClient)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(client);

        _client = client;
        _ownsClient = ownsClient;

        if (_client.BaseAddress is null)
        {
            _client.BaseAddress = new Uri(BaseUrl);
        }

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", credentials.AccessToken);

        if (_client.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _client.DefaultRequestHeaders.UserAgent.ParseAdd(DefaultUserAgent);
        }

        if (!_client.DefaultRequestHeaders.Contains("X-Yandex-Music-Client"))
        {
            _client.DefaultRequestHeaders.Add("X-Yandex-Music-Client", DefaultClientHeader);
        }
    }

    /// <summary>
    /// Issues a GET to <paramref name="path"/> with the supplied query parameters
    /// and deserialises the <c>result</c> field of the envelope to <typeparamref name="T"/>.
    /// </summary>
    public async Task<T> GetAsync<T>(
        string path,
        IReadOnlyDictionary<string, string?>? query = null,
        CancellationToken cancellationToken = default)
    {
        var url = BuildUri(path, query);
        using var response = await _client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        return await ReadEnvelopeAsync<T>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Issues a GET and returns the raw response body bytes.  Used for the
    /// encrypted audio stream download where the response is not JSON.
    /// </summary>
    public async Task<byte[]> GetBytesAsync(Uri url, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        using var response = await _client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new YandexMusicException($"GET {url} returned HTTP {(int)response.StatusCode}.")
            {
                StatusCode = response.StatusCode,
            };
        }
        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Uri BuildUri(string path, IReadOnlyDictionary<string, string?>? query)
    {
        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentException("Path must not be empty.", nameof(path));
        }

        var builder = new System.Text.StringBuilder(path);
        if (query is { Count: > 0 })
        {
            var separator = path.Contains('?', StringComparison.Ordinal) ? '&' : '?';
            foreach (var (key, value) in query)
            {
                if (value is null)
                {
                    continue;
                }
                builder.Append(separator)
                       .Append(Uri.EscapeDataString(key))
                       .Append('=')
                       .Append(Uri.EscapeDataString(value));
                separator = '&';
            }
        }
        return new Uri(builder.ToString(), UriKind.RelativeOrAbsolute);
    }

    private static async Task<T> ReadEnvelopeAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new YandexMusicException($"Request failed with HTTP {(int)response.StatusCode}.")
            {
                StatusCode = response.StatusCode,
                ResponseBody = body,
            };
        }

        var envelope = await response.Content
            .ReadFromJsonAsync<YandexResponse<T>>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (envelope is null || envelope.Result is null)
        {
            throw new YandexMusicException("Response envelope did not carry a 'result' object.")
            {
                StatusCode = response.StatusCode,
            };
        }

        return envelope.Result;
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
    }
}
