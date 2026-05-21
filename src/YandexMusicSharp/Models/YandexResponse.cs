using System.Text.Json.Serialization;

namespace YandexMusicSharp.Models;

/// <summary>
/// Standard envelope returned by api.music.yandex.net.  The interesting payload
/// lives under <c>result</c>; <c>invocationInfo</c> carries server diagnostics
/// (request id, timing) that we ignore.
/// </summary>
public sealed class YandexResponse<T>
{
    [JsonPropertyName("result")]
    public T? Result { get; init; }

    [JsonPropertyName("error")]
    public YandexErrorPayload? Error { get; init; }
}

public sealed class YandexErrorPayload
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

public sealed class Pager
{
    [JsonPropertyName("page")]
    public int Page { get; init; }

    [JsonPropertyName("perPage")]
    public int PerPage { get; init; }

    [JsonPropertyName("total")]
    public int Total { get; init; }
}
