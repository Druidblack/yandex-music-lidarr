namespace YandexMusicSharp;

/// <summary>
/// Quality tiers supported by the Yandex.Music get-file-info endpoint.  The string
/// values are sent verbatim in the <c>quality</c> query parameter.
/// </summary>
public enum YandexQuality
{
    /// <summary>AAC ~64 kbps - the freely available tier.</summary>
    Low,

    /// <summary>AAC ~192 kbps - the default tier for Plus accounts.</summary>
    Normal,

    /// <summary>FLAC - requires an active Yandex Plus subscription.</summary>
    Lossless,
}

public static class YandexQualityExtensions
{
    public static string ToApiString(this YandexQuality quality) => quality switch
    {
        YandexQuality.Low => "lq",
        YandexQuality.Normal => "nq",
        YandexQuality.Lossless => "lossless",
        _ => throw new ArgumentOutOfRangeException(nameof(quality), quality, "Unknown quality."),
    };
}
