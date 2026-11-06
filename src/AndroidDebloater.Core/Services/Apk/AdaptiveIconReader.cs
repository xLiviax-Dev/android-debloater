namespace AndroidDebloater.Core.Services.Apk;

/// <summary>
/// Liest aus einem adaptiven Icon (z. B. res/mipmap-anydpi-v26/ic_launcher.xml) den Verweis
/// auf Vorder- und Hintergrundzeichnung. Der Vordergrund hat Vorrang.
/// </summary>
internal static class AdaptiveIconReader
{
    public static uint? ReadDrawableReference(byte[] xml)
    {
        uint? background = null;

        foreach (var element in BinaryXmlReader.Read(xml))
        {
            bool foreground = string.Equals(element.Name, "foreground", StringComparison.Ordinal);
            bool isBackground = string.Equals(element.Name, "background", StringComparison.Ordinal);
            if (!foreground && !isBackground)
            {
                continue;
            }

            foreach (var attribute in element.Attributes)
            {
                if (!attribute.IsReference)
                {
                    continue;
                }

                if (foreground)
                {
                    return attribute.Data;
                }

                background ??= attribute.Data;
                break;
            }
        }

        return background;
    }
}
