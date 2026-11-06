using System.IO;
using AndroidDebloater.Core.Services.Apk;

namespace AndroidDebloater.Tests;

/// <summary>
/// Tests der APK-Parser gegen echte Fixtures des Testgeräts (Lenovo TB311FU, Android 16):
/// com.android.fmradio -> /system_ext/priv-app/FMRadio/FMRadio.apk.
/// </summary>
public class ApkParserTests
{
    /// <summary>Icon-Ressourcen-ID aus dem Manifest (Typ 0x0B = mipmap).</summary>
    private const uint FmRadioIconId = 0x7F0B0000;

    /// <summary>Label-Ressourcen-ID aus dem Manifest (Typ 0x0C = string).</summary>
    private const uint FmRadioLabelId = 0x7F0C0029;

    private static byte[] Fixture(string name)
        => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static byte[] Manifest() => Fixture("fmradio-AndroidManifest.xml");

    private static byte[] Arsc() => Fixture("fmradio-resources.arsc");

    [Fact]
    public void StringPool_ReadsManifestStrings()
    {
        var pool = new StringPool(Manifest(), 8);

        Assert.Equal(73, pool.Count);
        Assert.Equal("theme", pool.Get(0));
        Assert.Equal("com.android.fmradio", pool.Get(50));
    }

    [Fact]
    public void BinaryXmlReader_FindsApplicationElement_WithIconAndLabel()
    {
        var elements = BinaryXmlReader.Read(Manifest());

        var application = Assert.Single(elements, e => e.Name == "application");
        Assert.Contains(application.Attributes, a => a.ResourceId == ResChunk.AttrIcon);
        Assert.Contains(application.Attributes, a => a.ResourceId == ResChunk.AttrLabel);
    }

    [Fact]
    public void ManifestIconReader_ReadsIconAndLabelReferences()
    {
        var info = ManifestIconReader.Read(Manifest());

        Assert.Equal(FmRadioIconId, info.IconResourceId);
        Assert.Null(info.IconPath);
        Assert.Equal(FmRadioLabelId, info.LabelResourceId);
        Assert.Null(info.Label);
    }

    /// <summary>
    /// Regression: Offset 0 ist ein gültiger Tabelleneintrag (erster Eintrag an
    /// entriesStart); nur 0xFFFFFFFF markiert "kein Eintrag".
    /// </summary>
    [Fact]
    public void ArscResolver_ResolvesIcon_ToHighestDensityMipmap()
    {
        string? path = ArscResolver.Resolve(Arsc(), FmRadioIconId);

        Assert.Equal("res/mipmap-xxhdpi-v4/ic_launcher.png", path);
    }

    [Fact]
    public void ArscResolver_ResolvesLabelString()
    {
        string? label = ArscResolver.Resolve(Arsc(), FmRadioLabelId);

        Assert.Equal("FM Radio", label);
    }

    [Fact]
    public void ArscResolver_ReturnsNull_ForUnknownId()
    {
        Assert.Null(ArscResolver.Resolve(Arsc(), 0x7F0B00FF));
    }

    [Fact]
    public void ResChunk_DetectsImagePaths()
    {
        Assert.True(ResChunk.IsImagePath("res/mipmap-mdpi-v4/ic_launcher.png"));
        Assert.True(ResChunk.IsImagePath("res/drawable/icon.WEBP"));
        Assert.False(ResChunk.IsImagePath("res/drawable/icon.xml"));
    }

    [Fact]
    public void ResChunk_DetectsImageMagicBytes()
    {
        Assert.True(ResChunk.LooksLikeImage(new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4, 5, 6, 7, 8 }));
        Assert.True(ResChunk.LooksLikeImage(new byte[] { 0xFF, 0xD8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }));
        Assert.True(ResChunk.LooksLikeImage("RIFF\0\0\0\0WEBP"u8.ToArray()));
        Assert.False(ResChunk.LooksLikeImage(new byte[] { 0x3C, 0x3F, 0x78, 0x6D, 0x6C, 0, 0, 0, 0, 0, 0, 0 }));
    }
}
