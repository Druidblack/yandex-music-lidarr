using NUnit.Framework;

namespace YandexMusicSharp.Tests;

[TestFixture]
public class YandexQualityTests
{
    [TestCase(YandexQuality.Low, "lq")]
    [TestCase(YandexQuality.Normal, "nq")]
    [TestCase(YandexQuality.Lossless, "lossless")]
    public void ToApiString_MapsKnownTiers(YandexQuality input, string expected)
    {
        Assert.That(input.ToApiString(), Is.EqualTo(expected));
    }

    [Test]
    public void ToApiString_UnknownValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ((YandexQuality)99).ToApiString());
    }
}
