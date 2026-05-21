using System.Security.Cryptography;

namespace YandexMusicSharp.Codec;

/// <summary>
/// Decrypts the AES-CTR stream returned by the Yandex.Music get-file-info
/// <c>encraw</c> transport.  The IV is fixed at 16 zero bytes (12-byte nonce
/// followed by a 4-byte big-endian counter starting at zero), exactly the
/// scheme described by @keltecc and adopted by the ymd reference project.
///
/// Yandex returns keys of varying sizes depending on the track/quality - the
/// AES standard supports 128 / 192 / 256-bit keys, all valid here; .NET's
/// underlying Aes provider picks the right block cipher variant based on
/// <c>Aes.Key.Length</c>.
/// </summary>
public static class AesCtrDecryptor
{
    private const int BlockSize = 16;
    private static readonly int[] AllowedKeyLengths = { 16, 24, 32 };

    /// <summary>
    /// Decrypts the supplied ciphertext in one shot.  Suitable when the encrypted
    /// payload comfortably fits in memory (which is the typical case for a single
    /// audio track - a few MB to ~70 MB for hi-res FLAC).
    /// </summary>
    public static byte[] Decrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> ciphertext)
    {
        if (Array.IndexOf(AllowedKeyLengths, key.Length) < 0)
        {
            throw new ArgumentException(
                $"AES-CTR requires a 16, 24 or 32-byte key, got {key.Length}.",
                nameof(key));
        }

        if (ciphertext.IsEmpty)
        {
            return Array.Empty<byte>();
        }

        using var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = key.ToArray();

        using var encryptor = aes.CreateEncryptor();
        var output = new byte[ciphertext.Length];
        var counter = new byte[BlockSize];
        var keystream = new byte[BlockSize];

        for (var offset = 0; offset < ciphertext.Length; offset += BlockSize)
        {
            encryptor.TransformBlock(counter, 0, BlockSize, keystream, 0);

            var blockLength = Math.Min(BlockSize, ciphertext.Length - offset);
            for (var index = 0; index < blockLength; index++)
            {
                output[offset + index] = (byte)(ciphertext[offset + index] ^ keystream[index]);
            }

            IncrementBigEndianCounter(counter);
        }

        return output;
    }

    /// <summary>
    /// Convenience overload that accepts the AES key as the 64-character hex
    /// string the get-file-info response delivers it in.
    /// </summary>
    public static byte[] Decrypt(string keyHex, ReadOnlySpan<byte> ciphertext)
    {
        ArgumentNullException.ThrowIfNull(keyHex);
        return Decrypt(Convert.FromHexString(keyHex), ciphertext);
    }

    private static void IncrementBigEndianCounter(Span<byte> counter)
    {
        for (var index = counter.Length - 1; index >= 0; index--)
        {
            if (++counter[index] != 0)
            {
                return;
            }
        }
    }
}
