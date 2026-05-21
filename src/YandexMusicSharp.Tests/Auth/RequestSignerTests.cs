using NUnit.Framework;
using YandexMusicSharp.Auth;

namespace YandexMusicSharp.Tests.Auth;

[TestFixture]
public class RequestSignerTests
{
    // Reference vector generated from the same algorithm ymd uses:
    //   key       = "p93jhgh689SBReK6ghtw62"
    //   message   = f"{ts}{trackId}{quality}{codecs.replace(',', '')}{transports}"
    //   signature = base64(HMAC_SHA256(key, message))[:-1]
    //
    // The Python script under docs/yandex-music-downloader-research.md section 4
    // produces this exact pair for the canonical (ts=1700000000, trackId=123456)
    // input set.
    private const string ExpectedSignature = "pYG2f2k/SAgR2gHwDJZZ9hgX9kBKrRJUMwbf7fo1UkQ";

    [Test]
    public void Sign_KnownVector_MatchesYandexMusicDownloader()
    {
        var signature = RequestSigner.SignDownloadInfo(
            timestamp: 1_700_000_000,
            trackId: 123_456,
            quality: "lossless",
            codecs: "flac,flac-mp4,mp3,aac,he-aac,aac-mp4,he-aac-mp4",
            transports: "encraw");

        Assert.That(signature, Is.EqualTo(ExpectedSignature));
    }

    [Test]
    public void Sign_IsDeterministic_ForIdenticalInput()
    {
        var first = RequestSigner.SignDownloadInfo(1, 2, "lq", "mp3", "encraw");
        var second = RequestSigner.SignDownloadInfo(1, 2, "lq", "mp3", "encraw");

        Assert.That(first, Is.EqualTo(second));
    }

    [Test]
    public void Sign_DiffersWhenTimestampChanges()
    {
        var first = RequestSigner.SignDownloadInfo(1, 2, "lq", "mp3", "encraw");
        var second = RequestSigner.SignDownloadInfo(2, 2, "lq", "mp3", "encraw");

        Assert.That(first, Is.Not.EqualTo(second));
    }

    [Test]
    public void Sign_OutputLength_IsFortyThreeCharacters()
    {
        // HMAC-SHA256 yields 32 bytes; base64-encoded with one '=' padding (44 chars);
        // the ymd convention drops the final character so the resulting signature is 43.
        var signature = RequestSigner.SignDownloadInfo(0, 0, "lq", "mp3", "encraw");
        Assert.That(signature, Has.Length.EqualTo(43));
    }

    [Test]
    public void Sign_StripsCommasFromCodecList_BeforeHashing()
    {
        // If commas were left in, this would produce a different signature than the
        // reference implementation.  Asserting equality of "a,b,c" and "abc" inputs
        // protects against accidentally regressing the comma-stripping step.
        var withCommas = RequestSigner.SignDownloadInfo(1, 2, "lq", "flac,mp3,aac", "encraw");
        var withoutCommas = RequestSigner.SignDownloadInfo(1, 2, "lq", "flacmp3aac", "encraw");

        Assert.That(withCommas, Is.EqualTo(withoutCommas));
    }
}
