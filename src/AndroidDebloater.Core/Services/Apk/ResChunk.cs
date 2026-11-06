namespace AndroidDebloater.Core.Services.Apk;

/// <summary>
/// Konstanten und Kleinstleser für Androids binäre Ressourcenformate.
/// Alle Zahlen liegen Little Endian vor; Chunks beginnen mit
/// type(u16) headerSize(u16) size(u32).
/// </summary>
internal static class ResChunk
{
    // Chunk-Typen
    public const ushort TypeStringPool = 0x0001;
    public const ushort TypeTable = 0x0002;
    public const ushort TypeXml = 0x0003;
    public const ushort TypeXmlStartElement = 0x0102;
    public const ushort TypeXmlResourceMap = 0x0180;
    public const ushort TypeTablePackage = 0x0200;
    public const ushort TypeTableType = 0x0201;

    // Attribute aus android.R.attr
    public const uint AttrIcon = 0x01010002;
    public const uint AttrLabel = 0x01010001;

    // Res_value-Datentypen
    public const byte TypeReference = 0x01;
    public const byte TypeString = 0x03;
    public const byte TypeDynamicReference = 0x07;

    // Flags eines ResTable_type-Chunks
    public const byte TypeFlagSparse = 0x01;
    public const byte TypeFlagOffset16 = 0x02;
    public const uint NoEntry32 = 0xFFFFFFFF;
    public const ushort NoEntry16 = 0xFFFF;

    // Flags eines ResTable_entry
    public const ushort EntryFlagComplex = 0x0001;

    // Android 14+: kompakter Eintrag (8 Byte) mit direkt kodiertem Wert.
    // Der Datentyp steht im oberen Byte von flags: (dataType << 8) | EntryFlagCompact.
    public const ushort EntryFlagCompact = 0x0008;

    public static ushort U16(byte[] data, int offset)
        => (ushort)(data[offset] | (data[offset + 1] << 8));

    public static uint U32(byte[] data, int offset)
        => (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));

    /// <summary>Prüft, ob an der Position ein vollständiger Chunk des erwarteten Typs liegt.</summary>
    public static bool IsChunk(byte[] data, int offset, ushort expectedType)
        => offset >= 0
           && offset + 8 <= data.Length
           && U16(data, offset) == expectedType
           && (long)U32(data, offset + 4) <= data.Length - offset;

    /// <summary>Dateiendungen, die sich als Icon dekodieren lassen.</summary>
    public static bool IsImagePath(string path)
        => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
           || path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
           || path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
           || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase);

    /// <summary>Erkennt Bilddaten anhand ihrer Magic Bytes (PNG, JPEG, WebP).</summary>
    public static bool LooksLikeImage(byte[] data)
    {
        if (data.Length < 12)
        {
            return false;
        }

        bool png = data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47;
        bool jpeg = data[0] == 0xFF && data[1] == 0xD8;
        bool webp = data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F'
                    && data[8] == 'W' && data[9] == 'E' && data[10] == 'B' && data[11] == 'P';

        return png || jpeg || webp;
    }
}
