namespace AndroidDebloater.Core.Services.Apk;

/// <summary>Ein Attribut eines Elements aus einer binären Android-XML-Datei.</summary>
internal sealed record BinaryXmlAttribute(uint ResourceId, string? Name, byte DataType, uint Data, string? StringValue)
{
    public bool IsReference => DataType is ResChunk.TypeReference or ResChunk.TypeDynamicReference;
}

/// <summary>Ein Start-Element aus einer binären Android-XML-Datei.</summary>
internal sealed record BinaryXmlElement(string? Name, IReadOnlyList<BinaryXmlAttribute> Attributes);

/// <summary>
/// Liest binäre Android-XML (Chunk-Typ 0x0003), wie sie in APKs liegt
/// (AndroidManifest.xml oder Ressourcen-XMLs) und liefert die Start-Elemente samt Attributen.
/// </summary>
internal static class BinaryXmlReader
{
    public static List<BinaryXmlElement> Read(byte[] data)
    {
        var elements = new List<BinaryXmlElement>();
        if (!ResChunk.IsChunk(data, 0, ResChunk.TypeXml))
        {
            return elements;
        }

        StringPool? pool = null;
        uint[]? resourceMap = null;

        int offset = ResChunk.U16(data, 2);
        int end = Math.Min(data.Length, (int)ResChunk.U32(data, 4));

        while (offset + 8 <= end)
        {
            ushort type = ResChunk.U16(data, offset);
            int headerSize = ResChunk.U16(data, offset + 2);
            long chunkSize = ResChunk.U32(data, offset + 4);
            if (chunkSize < 8 || offset + chunkSize > data.Length)
            {
                break;
            }

            switch (type)
            {
                case ResChunk.TypeStringPool:
                    pool = new StringPool(data, offset);
                    break;

                case ResChunk.TypeXmlResourceMap:
                    {
                        int count = Math.Max((int)(chunkSize - headerSize) / 4, 0);
                        resourceMap = new uint[count];
                        for (int i = 0; i < count; i++)
                        {
                            resourceMap[i] = ResChunk.U32(data, offset + headerSize + i * 4);
                        }

                        break;
                    }

                case ResChunk.TypeXmlStartElement:
                    var element = ReadElement(data, offset, headerSize, (int)chunkSize, pool, resourceMap);
                    if (element is not null)
                    {
                        elements.Add(element);
                    }

                    break;
            }

            offset += (int)chunkSize;
        }

        return elements;
    }

    private static BinaryXmlElement? ReadElement(
        byte[] data,
        int chunkOffset,
        int headerSize,
        int chunkSize,
        StringPool? pool,
        uint[]? resourceMap)
    {
        // Nach dem 16 Byte langen Chunk-Kopf steht ResXMLTree_attrExt.
        int ext = chunkOffset + headerSize;
        if (ext + 20 > data.Length)
        {
            return null;
        }

        string? name = pool?.Get((int)ResChunk.U32(data, ext + 4));
        int attributeStart = ResChunk.U16(data, ext + 8);
        int attributeSize = ResChunk.U16(data, ext + 10);
        int attributeCount = ResChunk.U16(data, ext + 12);

        if (attributeSize < 20)
        {
            return null;
        }

        int limit = Math.Min(chunkOffset + chunkSize, data.Length);
        var attributes = new List<BinaryXmlAttribute>(attributeCount);

        for (int i = 0; i < attributeCount; i++)
        {
            int attribute = ext + attributeStart + i * attributeSize;
            if (attribute + 20 > limit)
            {
                break;
            }

            uint nameIndex = ResChunk.U32(data, attribute + 4);
            byte dataType = data[attribute + 15];
            uint value = ResChunk.U32(data, attribute + 16);

            // Der Name ist meist ein StringPool-Index, der über die Resource-Map auf eine
            // Ressourcen-ID zeigt. Manche Manifeste tragen die ID direkt ein.
            uint resourceId = 0;
            if (resourceMap is not null && nameIndex < resourceMap.Length && resourceMap[nameIndex] != 0)
            {
                resourceId = resourceMap[nameIndex];
            }
            else if (nameIndex >= 0x01000000)
            {
                resourceId = nameIndex;
            }

            string? attributeName = pool?.Get((int)nameIndex);
            string? stringValue = dataType == ResChunk.TypeString ? pool?.Get((int)value) : null;

            attributes.Add(new BinaryXmlAttribute(resourceId, attributeName, dataType, value, stringValue));
        }

        return new BinaryXmlElement(name, attributes);
    }
}
