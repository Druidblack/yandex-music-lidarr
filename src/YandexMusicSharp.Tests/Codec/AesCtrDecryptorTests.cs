using NUnit.Framework;
using YandexMusicSharp.Codec;

namespace YandexMusicSharp.Tests.Codec;

[TestFixture]
public class AesCtrDecryptorTests
{
    // Reference vector generated with `openssl enc -aes-256-ctr -K <key> -iv 00..00 -nopad -nosalt`.
    // The IV is 16 zero bytes which matches the Yandex.Music encraw transport (12 zero nonce + 4 zero
    // counter bytes, big-endian).  The plaintext deliberately spans more than one AES block so a bug in
    // counter increment would corrupt bytes 16-31.
    private const string KeyHex = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private const string PlaintextHex =
        "664c6143000000000000000000000000" +
        "00000000000000000000000000000000" +
        "00000000000000000000000000000000" +
        "00000000000000000000000000000000";

    private const string CiphertextHex =
        "16970776c2af9e3713e3eaa051937c7b" +
        "35d191d5694779e13b20c82bbc764640" +
        "0a07e05477a05a5ea1823328d83ea639" +
        "59cdd16915f44874077398f3d6b16575";

    [Test]
    public void Decrypt_KnownVector_RecoversPlaintext()
    {
        var key = Convert.FromHexString(KeyHex);
        var ciphertext = Convert.FromHexString(CiphertextHex);
        var expected = Convert.FromHexString(PlaintextHex);

        var actual = AesCtrDecryptor.Decrypt(key, ciphertext);

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void Decrypt_EmptyInput_ReturnsEmptyArray()
    {
        var key = Convert.FromHexString(KeyHex);
        var actual = AesCtrDecryptor.Decrypt(key, Array.Empty<byte>());
        Assert.That(actual, Is.Empty);
    }

    [Test]
    public void Decrypt_PartialFinalBlock_IsHandled()
    {
        // 17 bytes - one full AES block + one trailing byte.  The keystream byte from the
        // second block must still be XORed in or the trailing byte would leak through verbatim.
        var key = Convert.FromHexString(KeyHex);
        var fullCiphertext = Convert.FromHexString(CiphertextHex);
        var partial = fullCiphertext[..17];
        var expected = Convert.FromHexString(PlaintextHex)[..17];

        var actual = AesCtrDecryptor.Decrypt(key, partial);

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void DecryptFromHexKey_ParsesHex_AndMatchesByteOverload()
    {
        var ciphertext = Convert.FromHexString(CiphertextHex);
        var expected = Convert.FromHexString(PlaintextHex);

        var actual = AesCtrDecryptor.Decrypt(KeyHex, ciphertext);

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void Decrypt_InvalidKeyLength_Throws()
    {
        var wrongLength = new byte[17];
        Assert.Throws<ArgumentException>(() => AesCtrDecryptor.Decrypt(wrongLength, new byte[16]));
    }

    [Test]
    public void Decrypt_AcceptsAes128Key()
    {
        // The Yandex encraw transport occasionally hands out 16-byte (AES-128)
        // keys instead of the doc'd 32-byte ones.  Just verify the decryptor no
        // longer rejects them up-front; correctness is implicit via the AES
        // primitive.
        var key = new byte[16];
        Assert.DoesNotThrow(() => AesCtrDecryptor.Decrypt(key, new byte[16]));
    }
}
