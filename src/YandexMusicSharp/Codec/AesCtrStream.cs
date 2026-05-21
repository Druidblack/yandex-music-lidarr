using System.Security.Cryptography;

namespace YandexMusicSharp.Codec;

/// <summary>
/// Streaming AES-CTR transformer.  Suitable for decrypting an HTTP response
/// body chunk-by-chunk without buffering the whole encrypted payload in memory
/// first.  The cipher state (counter + leftover keystream byte position) is
/// preserved between calls so the user can feed arbitrary-sized chunks.
///
/// IV layout matches Yandex's encraw transport: 12 zero nonce bytes followed
/// by a 4-byte big-endian counter starting at zero.  Counter increments
/// happen across the whole 16-byte block, which is equivalent for any payload
/// under the 2^96-block ceiling.
/// </summary>
public sealed class AesCtrStream : IDisposable
{
    private const int BlockSize = 16;

    private readonly Aes _aes;
    private readonly ICryptoTransform _encryptor;
    private readonly byte[] _counter = new byte[BlockSize];
    private readonly byte[] _keystream = new byte[BlockSize];

    /// <summary>Offset within the current keystream block.  16 means "exhausted, generate next".</summary>
    private int _keystreamPosition = BlockSize;

    public AesCtrStream(ReadOnlySpan<byte> key)
    {
        _aes = Aes.Create();
        _aes.Mode = CipherMode.ECB;
        _aes.Padding = PaddingMode.None;
        _aes.Key = key.ToArray();
        _encryptor = _aes.CreateEncryptor();
    }

    /// <summary>
    /// XORs the supplied buffer in place with keystream bytes derived from the
    /// running counter.  Same operation works for encryption and decryption.
    /// </summary>
    public void Transform(Span<byte> data)
    {
        var position = 0;

        // Consume any leftover keystream bytes from the previous call first.
        if (_keystreamPosition < BlockSize)
        {
            var available = BlockSize - _keystreamPosition;
            var take = Math.Min(available, data.Length);
            for (var i = 0; i < take; i++)
            {
                data[i] ^= _keystream[_keystreamPosition + i];
            }
            _keystreamPosition += take;
            position += take;
        }

        // Process full 16-byte blocks.
        while (data.Length - position >= BlockSize)
        {
            GenerateKeystreamBlock();
            for (var i = 0; i < BlockSize; i++)
            {
                data[position + i] ^= _keystream[i];
            }
            position += BlockSize;
            _keystreamPosition = BlockSize;
        }

        // Process the trailing partial block, retaining unused keystream bytes
        // for the next call.
        if (position < data.Length)
        {
            GenerateKeystreamBlock();
            var remaining = data.Length - position;
            for (var i = 0; i < remaining; i++)
            {
                data[position + i] ^= _keystream[i];
            }
            _keystreamPosition = remaining;
        }
    }

    private void GenerateKeystreamBlock()
    {
        _encryptor.TransformBlock(_counter, 0, BlockSize, _keystream, 0);
        for (var index = _counter.Length - 1; index >= 0; index--)
        {
            if (++_counter[index] != 0)
            {
                return;
            }
        }
    }

    public void Dispose()
    {
        _encryptor.Dispose();
        _aes.Dispose();
    }
}
