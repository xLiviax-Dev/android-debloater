using System.Text;

namespace AndroidDebloater.Core.Services.Apk;

/// <summary>
/// Liest einen StringPool-Chunk (0x0001). Android speichert Texte je nach Build
/// als UTF-8 oder UTF-16 – beide Varianten werden unterstützt.
/// </summary>
internal sealed class StringPool
{
    private readonly string?[] _strings;

    public StringPool(byte[] data, int chunkOffset)
    {
        int stringCount = (int)ResChunk.U32(data, chunkOffset + 8);
        int flags = (int)ResChunk.U32(data, chunkOffset + 16);
        int stringsStart = (int)ResChunk.U32(data, chunkOffset + 20);
        bool utf8 = (flags & 0x0100) != 0;

        _strings = new string?[Math.Max(stringCount, 0)];
        int offsetsBase = chunkOffset + 28;
        int dataBase = chunkOffset + stringsStart;

        for (int i = 0; i < _strings.Length; i++)
        {
            int offset = dataBase + (int)ResChunk.U32(data, offsetsBase + i * 4);
            try
            {
                _strings[i] = utf8 ? ReadUtf8(data, ref offset) : ReadUtf16(data, ref offset);
            }
            catch
            {
                _strings[i] = null;
            }
        }
    }

    public int Count => _strings.Length;

    public string? Get(int index)
        => index >= 0 && index < _strings.Length ? _strings[index] : null;

    private static string ReadUtf8(byte[] data, ref int offset)
    {
        _ = ReadLength8(data, ref offset);              // Zeichenlänge (nicht benötigt)
        int byteLength = ReadLength8(data, ref offset);
        string value = byteLength > 0 ? Encoding.UTF8.GetString(data, offset, byteLength) : string.Empty;
        offset += byteLength + 1;                       // Null-Terminator
        return value;
    }

    private static string ReadUtf16(byte[] data, ref int offset)
    {
        int length = data[offset] | (data[offset + 1] << 8);
        offset += 2;

        if ((length & 0x8000) != 0)
        {
            length = ((length & 0x7FFF) << 16) | (data[offset] | (data[offset + 1] << 8));
            offset += 2;
        }

        string value = length > 0 ? Encoding.Unicode.GetString(data, offset, length * 2) : string.Empty;
        offset += length * 2 + 2;                       // Null-Terminator
        return value;
    }

    /// <summary>Länge ist 1 Byte oder 2 Bytes, wenn im ersten Byte das Höchstbit gesetzt ist.</summary>
    private static int ReadLength8(byte[] data, ref int offset)
    {
        int first = data[offset++];
        if ((first & 0x80) == 0)
        {
            return first;
        }

        int second = data[offset++];
        return ((first & 0x7F) << 8) | second;
    }
}
