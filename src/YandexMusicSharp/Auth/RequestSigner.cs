using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace YandexMusicSharp.Auth;

/// <summary>
/// Produces the HMAC-SHA256 signatures that the Yandex.Music get-file-info and lyrics
/// endpoints require.  The shared secret <see cref="DefaultSignKey"/> was extracted from
/// the official Android client by the MarshalX/yandex-music-api project and is the same
/// key the Python reference implementation (ymd) uses.
/// </summary>
public static class RequestSigner
{
    public const string DefaultSignKey = "p93jhgh689SBReK6ghtw62";

    /// <summary>
    /// Signs a get-file-info request.  Reproduces the ymd recipe exactly:
    /// concatenate the parameter values in canonical order, strip commas (so the
    /// comma-separated codec list contributes its raw concatenation), HMAC-SHA256
    /// with <see cref="DefaultSignKey"/>, base64-encode the digest and drop the
    /// final character (the Python <c>[:-1]</c> slice).
    /// </summary>
    public static string SignDownloadInfo(
        long timestamp,
        long trackId,
        string quality,
        string codecs,
        string transports)
    {
        var message = string.Concat(
            timestamp.ToString(CultureInfo.InvariantCulture),
            trackId.ToString(CultureInfo.InvariantCulture),
            quality,
            codecs,
            transports);
        return ComputeSignature(message, dropTrailingCharacter: true);
    }

    /// <summary>
    /// Signs a lyrics request.  The lyrics endpoint expects the unmodified base64
    /// signature (no trailing-character drop) and the message is the concatenation of
    /// the track id followed by the timestamp.
    /// </summary>
    public static string SignLyrics(long trackId, long timestamp)
    {
        var message = string.Concat(
            trackId.ToString(CultureInfo.InvariantCulture),
            timestamp.ToString(CultureInfo.InvariantCulture));
        return ComputeSignature(message, dropTrailingCharacter: false);
    }

    private static string ComputeSignature(string message, bool dropTrailingCharacter)
    {
        var keyBytes = Encoding.UTF8.GetBytes(DefaultSignKey);
        var messageBytes = Encoding.UTF8.GetBytes(message.Replace(",", string.Empty, StringComparison.Ordinal));
        var digest = HMACSHA256.HashData(keyBytes, messageBytes);
        var encoded = Convert.ToBase64String(digest);
        return dropTrailingCharacter ? encoded[..^1] : encoded;
    }
}
