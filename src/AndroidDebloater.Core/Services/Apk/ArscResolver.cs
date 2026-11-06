namespace AndroidDebloater.Core.Services.Apk;

/// <summary>Zerlegte Ressourcen-ID: Paket-, Typ- und Eintragsnummer.</summary>
internal readonly record struct ResourceKey(byte Package, byte Type, int Entry);

/// <summary>
/// Löst eine Ressourcen-ID (z. B. 0x7f080001) über die resources.arsc in ihren Wert auf,
/// etwa den Dateipfad res/mipmap-xxhdpi/ic_launcher.png oder einen Text wie "Chrome".
/// Unter mehreren Varianten gewinnt ein echtes Bild vor einer XML-Datei und eine höhere
/// Bildschirmdichte vor einer niedrigeren.
/// </summary>
internal static class ArscResolver
{
    private const int ImageBonus = 1_000_000;

    public static string? Resolve(byte[] arsc, uint resourceId)
    {
        if (!ResChunk.IsChunk(arsc, 0, ResChunk.TypeTable))
        {
            return null;
        }

        var target = new ResourceKey(
            Package: (byte)((resourceId >> 24) & 0xFF),
            Type: (byte)((resourceId >> 16) & 0xFF),
            Entry: (int)(resourceId & 0xFFFF));

        StringPool? valueStrings = null;
        string? bestPath = null;
        int bestScore = int.MinValue;

        int offset = ResChunk.U16(arsc, 2);
        int end = Math.Min(arsc.Length, (int)ResChunk.U32(arsc, 4));

        while (offset + 8 <= end)
        {
            ushort type = ResChunk.U16(arsc, offset);
            long chunkSize = ResChunk.U32(arsc, offset + 4);
            if (chunkSize < 8 || offset + chunkSize > arsc.Length)
            {
                break;
            }

            if (type == ResChunk.TypeStringPool)
            {
                // Der globale StringPool steht vor den Paketen und hält die Werte.
                valueStrings ??= new StringPool(arsc, offset);
            }
            else if (type == ResChunk.TypeTablePackage && valueStrings is not null)
            {
                CollectFromPackage(arsc, offset, (int)chunkSize, target, valueStrings, ref bestPath, ref bestScore);
            }

            offset += (int)chunkSize;
        }

        return bestPath;
    }

    private static void CollectFromPackage(
        byte[] arsc,
        int packageOffset,
        int packageSize,
        ResourceKey target,
        StringPool valueStrings,
        ref string? bestPath,
        ref int bestScore)
    {
        if ((byte)ResChunk.U32(arsc, packageOffset + 8) != target.Package)
        {
            return;
        }

        int headerSize = ResChunk.U16(arsc, packageOffset + 2);
        int offset = packageOffset + headerSize;
        int end = Math.Min(arsc.Length, packageOffset + packageSize);

        while (offset + 8 <= end)
        {
            ushort type = ResChunk.U16(arsc, offset);
            long chunkSize = ResChunk.U32(arsc, offset + 4);
            if (chunkSize < 8 || offset + chunkSize > arsc.Length)
            {
                break;
            }

            if (type == ResChunk.TypeTableType && (byte)ResChunk.U32(arsc, offset + 8) == target.Type)
            {
                ConsiderType(arsc, offset, target.Entry, valueStrings, ref bestPath, ref bestScore);
            }

            offset += (int)chunkSize;
        }
    }

    private static void ConsiderType(
        byte[] arsc,
        int typeOffset,
        int entryIndex,
        StringPool valueStrings,
        ref string? bestPath,
        ref int bestScore)
    {
        string? path = ReadTypeValue(arsc, typeOffset, entryIndex, valueStrings);
        if (path is null)
        {
            return;
        }

        // Bilder gewinnen nach Dichte, Texte nach der neutralen Sprache (language == 0).
        int score = ResChunk.IsImagePath(path)
            ? ImageBonus + ReadDensity(arsc, typeOffset)
            : IsDefaultLocale(arsc, typeOffset) ? 1 : 0;

        if (score > bestScore)
        {
            bestScore = score;
            bestPath = path;
        }
    }

    /// <summary>
    /// Liest den Wert genau dieses Type-Chunks für den angegebenen Eintrag.
    /// Gibt null zurück, wenn der Eintrag fehlt, komplex ist oder keinen String enthält.
    /// </summary>
    internal static string? ReadTypeValue(byte[] arsc, int typeOffset, int entryIndex, StringPool valueStrings)
    {
        if (!TryFindEntry(arsc, typeOffset, entryIndex, out int entry))
        {
            return null;
        }

        // Die Flags liegen in beiden Eintragsformaten an derselben Stelle (Offset +2).
        ushort flags = ResChunk.U16(arsc, entry + 2);

        if ((flags & ResChunk.EntryFlagCompact) != 0)
        {
            // Kompakter Eintrag (Android 14+): key(u16) flags(u16) data(u32). Der Datentyp
            // steckt im oberen Byte der Flags, kein separates Res_value vorhanden.
            if ((byte)(flags >> 8) != ResChunk.TypeString)
            {
                return null;
            }

            string? compact = valueStrings.Get((int)ResChunk.U32(arsc, entry + 4));
            return string.IsNullOrEmpty(compact) ? null : compact;
        }

        if ((flags & ResChunk.EntryFlagComplex) != 0)
        {
            return null;
        }

        // Klassischer Eintrag: nach dem 8-Byte-Kopf folgt ein Res_value (size,res0,dataType,data).
        if (arsc[entry + 11] != ResChunk.TypeString)
        {
            return null;
        }

        string? value = valueStrings.Get((int)ResChunk.U32(arsc, entry + 12));
        return string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>
    /// Ermittelt die absolute Position des ResTable_entry für den angegebenen Eintrag
    /// dieses Type-Chunks. Berücksichtigt alle drei Offsettabelle-Kodierungen.
    /// </summary>
    private static bool TryFindEntry(byte[] arsc, int typeOffset, int entryIndex, out int entry)
    {
        entry = 0;

        long entryCount = ResChunk.U32(arsc, typeOffset + 12);
        int entriesStart = (int)ResChunk.U32(arsc, typeOffset + 16);
        byte flags = arsc[typeOffset + 9];

        if (entryCount <= 0 || entryCount > 1_000_000 || entryIndex < 0)
        {
            return false;
        }

        int offsetsBase = typeOffset + ResChunk.U16(arsc, typeOffset + 2);
        if (offsetsBase + 8 > arsc.Length)
        {
            return false;
        }

        // Die Eintragstabelle liegt je nach Android-Version in drei Kodierungen vor;
        // "kein Eintrag" ist je Kodierung anders markiert.
        int? entryOffset;
        if ((flags & ResChunk.TypeFlagSparse) != 0)
        {
            entryOffset = ReadSparseOffset(arsc, offsetsBase, entryCount, entryIndex);
        }
        else if ((flags & ResChunk.TypeFlagOffset16) != 0)
        {
            entryOffset = ReadOffset16(arsc, offsetsBase, entryCount, entryIndex);
        }
        else
        {
            entryOffset = ReadOffset32(arsc, offsetsBase, entryCount, entryIndex);
        }

        if (entryOffset is not int offsetInType)
        {
            return false;
        }

        int position = typeOffset + entriesStart + offsetInType;
        if (position < 0 || position + 16 > arsc.Length)
        {
            return false;
        }

        entry = position;
        return true;
    }

    /// <summary>
    /// Klassische 32-Bit-Offsettabelle; 0xFFFFFFFF bedeutet kein Eintrag.
    /// Der Offset 0 ist gültig (erster Eintrag direkt an entriesStart).
    /// </summary>
    private static int? ReadOffset32(byte[] arsc, int offsetsBase, long entryCount, int entryIndex)
    {
        if (entryIndex >= entryCount || offsetsBase + entryCount * 4 > arsc.Length)
        {
            return null;
        }

        uint raw = ResChunk.U32(arsc, offsetsBase + entryIndex * 4);
        return raw == ResChunk.NoEntry32 ? null : (int)raw;
    }

    /// <summary>
    /// FLAG_OFFSET16: 2-Byte-Offsets, gespeichert in 4-Byte-Einheiten (max. 256 KB je Typ).
    /// 0xFFFF bedeutet kein Eintrag; 0 ist ein gültiger Offset.
    /// </summary>
    private static int? ReadOffset16(byte[] arsc, int offsetsBase, long entryCount, int entryIndex)
    {
        if (entryIndex >= entryCount || offsetsBase + entryCount * 2 > arsc.Length)
        {
            return null;
        }

        ushort raw = ResChunk.U16(arsc, offsetsBase + entryIndex * 2);
        return raw == ResChunk.NoEntry16 ? null : raw * 4;
    }

    /// <summary>FLAG_SPARSE: Paare aus Eintragsnummer und 4-Byte-Offset, aufsteigend sortiert.</summary>
    private static int? ReadSparseOffset(byte[] arsc, int offsetsBase, long entryCount, int entryIndex)
    {
        if (offsetsBase + entryCount * 4 > arsc.Length)
        {
            return null;
        }

        for (int i = 0; i < entryCount; i++)
        {
            int pair = offsetsBase + i * 4;
            int index = ResChunk.U16(arsc, pair);
            if (index == entryIndex)
            {
                return ResChunk.U16(arsc, pair + 2) * 4;
            }

            if (index > entryIndex)
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>Prüft, ob der Type-Chunk zur neutralen Sprache gehört (kein Sprach-/Länderkürzel).</summary>
    private static bool IsDefaultLocale(byte[] arsc, int typeOffset)
    {
        int config = typeOffset + 20;
        return config + 12 <= arsc.Length && arsc[config + 8] == 0 && arsc[config + 9] == 0;
    }

    /// <summary>Liest die Bildschirmdichte aus der ResTable_config, falls der Kopf sie enthält.</summary>
    private static int ReadDensity(byte[] arsc, int typeOffset)
    {
        int config = typeOffset + 20;            // fester Kopfanteil des Type-Chunks
        if (config + 16 > arsc.Length)
        {
            return 0;
        }

        if ((int)ResChunk.U32(arsc, config) < 16) // configSize zu klein -> kein Dichtefeld
        {
            return 0;
        }

        int density = ResChunk.U16(arsc, config + 14);
        return density is 0xFFFF or 0xFFFE ? 0 : density;   // DEFAULT / ANY
    }
}
