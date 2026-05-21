using NUnit.Framework;

namespace YandexMusicSharp.Tests;

[TestFixture]
public class DownloadAddressTests
{
    [Test]
    public void Parse_StandardUrl_ReturnsAlbumIdAndQuality()
    {
        var address = DownloadAddress.Parse("yandexmusic://album/12345/quality/lossless");

        Assert.That(address.AlbumId, Is.EqualTo(12345));
        Assert.That(address.Quality, Is.EqualTo(YandexQuality.Lossless));
    }

    [Test]
    public void Parse_IsCaseInsensitive_OnSchemeAndQuality()
    {
        var address = DownloadAddress.Parse("YandexMusic://album/42/quality/NORMAL");
        Assert.That(address.AlbumId, Is.EqualTo(42));
        Assert.That(address.Quality, Is.EqualTo(YandexQuality.Normal));
    }

    [Test]
    public void Parse_NonNumericAlbumId_Throws()
    {
        Assert.Throws<FormatException>(() => DownloadAddress.Parse("yandexmusic://album/foo/quality/lossless"));
    }

    [Test]
    public void Parse_UnknownQuality_Throws()
    {
        Assert.Throws<FormatException>(() => DownloadAddress.Parse("yandexmusic://album/1/quality/turbo"));
    }

    [Test]
    public void Parse_MissingQualitySegment_Throws()
    {
        Assert.Throws<FormatException>(() => DownloadAddress.Parse("yandexmusic://album/1"));
    }

    [Test]
    public void Parse_WrongScheme_Throws()
    {
        Assert.Throws<FormatException>(() => DownloadAddress.Parse("http://album/1/quality/low"));
    }

    [Test]
    public void Parse_EmptyInput_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => DownloadAddress.Parse(string.Empty));
    }

    [Test]
    public void ToString_RoundTripsThroughParse()
    {
        var original = new DownloadAddress(987_654, YandexQuality.Lossless);

        var roundTripped = DownloadAddress.Parse(original.ToString());

        Assert.That(roundTripped, Is.EqualTo(original));
    }

    [Test]
    public void ToString_EmitsLowercaseQuality()
    {
        var address = new DownloadAddress(1, YandexQuality.Low);
        Assert.That(address.ToString(), Is.EqualTo("yandexmusic://album/1/quality/low"));
    }
}
