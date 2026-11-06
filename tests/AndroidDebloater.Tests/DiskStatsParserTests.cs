using AndroidDebloater.Core.Services;

namespace AndroidDebloater.Tests;

/// <summary>
/// Tests des Größen-Parsers gegen das am Testgerät verifizierte Format von
/// "adb shell dumpsys diskstats" (vier indexgleiche Listen in eckigen Klammern).
/// </summary>
public class DiskStatsParserTests
{
    private const string Dump = """
        Latency: 1ms [512B Data Write]
        App Size: 13501441024
        App Data Size: 4784798710
        App Cache Size: 582401024
        Package Names: ["com.android.fmradio","com.example.app","com.example.empty"]
        App Sizes: [1089024,3837952,12288]
        App Data Sizes: [29184,24576,0]
        Cache Sizes: [14336,0,0]
        """;

    [Fact]
    public void Parse_MapsSizesByPackageName()
    {
        var sizes = DiskStatsParser.Parse(Dump);

        Assert.Equal(3, sizes.Count);

        var fmradio = sizes["com.android.fmradio"];
        Assert.Equal(1089024, fmradio.ApkBytes);
        Assert.Equal(29184, fmradio.DataBytes);
        Assert.Equal(14336, fmradio.CacheBytes);
    }

    [Fact]
    public void Parse_ReadsZeroValues()
    {
        var sizes = DiskStatsParser.Parse(Dump);

        var empty = sizes["com.example.empty"];
        Assert.Equal(12288, empty.ApkBytes);
        Assert.Equal(0, empty.DataBytes);
        Assert.Equal(0, empty.CacheBytes);
    }

    [Fact]
    public void Parse_ReturnsEmpty_WhenNoPackageList()
    {
        var sizes = DiskStatsParser.Parse("Latency: 1ms [512B Data Write]");

        Assert.Empty(sizes);
    }

    [Fact]
    public void Parse_ReturnsEmpty_ForEmptyInput()
    {
        Assert.Empty(DiskStatsParser.Parse(string.Empty));
    }
}
