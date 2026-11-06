namespace AndroidDebloater.Core.Services.Apk;

/// <summary>Icon-Verweis und Anzeigename aus dem AndroidManifest.xml.</summary>
internal readonly record struct ManifestIconInfo(
    uint? IconResourceId,
    string? IconPath,
    string? Label,
    uint? LabelResourceId);

/// <summary>Sucht im AndroidManifest.xml das Launcher-Icon und den Anzeigenamen.</summary>
internal static class ManifestIconReader
{
    public static ManifestIconInfo Read(byte[] manifest)
    {
        uint? iconResourceId = null;
        string? iconPath = null;
        string? label = null;
        uint? labelResourceId = null;

        foreach (var element in BinaryXmlReader.Read(manifest))
        {
            // Nur <application> zählt: permission- und activity-Elemente tragen ebenfalls
            // ein android:label (lokalisiert) bzw. android:icon und würden sonst gewinnen.
            if (element.Name != "application")
            {
                continue;
            }

            foreach (var attribute in element.Attributes)
            {
                if (IsAttribute(attribute, ResChunk.AttrIcon, "icon"))
                {
                    if (attribute.DataType == ResChunk.TypeString)
                    {
                        iconPath ??= attribute.StringValue;
                    }
                    else if (attribute.IsReference)
                    {
                        iconResourceId ??= attribute.Data;
                    }
                }
                else if (IsAttribute(attribute, ResChunk.AttrLabel, "label"))
                {
                    if (attribute.DataType == ResChunk.TypeString)
                    {
                        label ??= attribute.StringValue;
                    }
                    else if (attribute.IsReference)
                    {
                        labelResourceId ??= attribute.Data;
                    }
                }
            }

            break;
        }

        return new ManifestIconInfo(iconResourceId, iconPath, label, labelResourceId);
    }

    private static bool IsAttribute(BinaryXmlAttribute attribute, uint resourceId, string name)
        => attribute.ResourceId == resourceId
           || string.Equals(attribute.Name, name, StringComparison.Ordinal);
}
