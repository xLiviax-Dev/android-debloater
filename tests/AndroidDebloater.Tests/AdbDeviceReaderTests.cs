using AndroidDebloater.Core.Services;

namespace AndroidDebloater.Tests;

/// <summary>
/// Tests der Geräteerkennung gegen die am Testgerät (Lenovo TB311FU) verifizierten
/// Formate von "adb devices -l" und "adb shell getprop".
/// </summary>
public class AdbDeviceReaderTests
{
    private const string DevicesOutput = """
        List of devices attached
        TEST1234               device product:TB311FU_EEA model:TB311FU device:TB311FU transport_id:1
        """;

    [Fact]
    public void Parse_ReadsOnlineDeviceWithAttributes()
    {
        var devices = AdbDeviceReader.Parse(DevicesOutput);

        var device = Assert.Single(devices);
        Assert.Equal("TEST1234", device.Serial);
        Assert.Equal("device", device.State);
        Assert.Equal("TB311FU", device.Model);
        Assert.Equal("TB311FU_EEA", device.Product);
        Assert.True(device.IsOnline);
        Assert.False(device.IsUnauthorized);
        Assert.Equal("TB311FU (TEST1234)", device.DisplayName);
    }

    [Fact]
    public void Parse_SkipsDaemonNotice()
    {
        const string output = """
            * daemon not running; starting now at tcp:5037
            * daemon started successfully
            List of devices attached
            1234567890             unauthorized
            """;

        var devices = AdbDeviceReader.Parse(output);

        var device = Assert.Single(devices);
        Assert.True(device.IsUnauthorized);
        Assert.False(device.IsOnline);
    }

    [Fact]
    public void Parse_SkipsHeader_WhenNoDevices()
    {
        Assert.Empty(AdbDeviceReader.Parse("List of devices attached\n"));
    }
}

/// <summary>Tests des getprop-Parsers (Eigenschaften im Format "[key]: [value]").</summary>
public class DeviceInfoReaderTests
{
    private const string GetPropOutput = """
        [ro.product.manufacturer]: [LENOVO]
        [ro.product.model]: [TB311FU]
        [ro.build.version.release]: [16]
        [ro.build.version.sdk]: [36]
        [ro.build.id]: [BP2A.250605.031.A3]
        [ro.product.vendor.model]: [TB311FU]
        """;

    [Fact]
    public void Parse_ReadsDeviceAttributes()
    {
        var info = DeviceInfoReader.Parse(GetPropOutput, "TEST1234");

        Assert.Equal("TEST1234", info.Serial);
        Assert.Equal("LENOVO", info.Manufacturer);
        Assert.Equal("TB311FU", info.Model);
        Assert.Equal("16", info.AndroidVersion);
        Assert.Equal(36, info.SdkLevel);
        Assert.Equal("BP2A.250605.031.A3", info.BuildId);
    }

    [Fact]
    public void Parse_FallsBackToVendorModel()
    {
        const string output = "[ro.product.vendor.model]: [TB311FU]";

        var info = DeviceInfoReader.Parse(output, "TEST1234");

        Assert.Equal("TB311FU", info.Model);
    }

    [Fact]
    public void Parse_LeavesMissingValuesEmpty()
    {
        var info = DeviceInfoReader.Parse(string.Empty, "TEST1234");

        Assert.Null(info.Manufacturer);
        Assert.Null(info.Model);
        Assert.Null(info.AndroidVersion);
        Assert.Null(info.SdkLevel);
        Assert.Null(info.BuildId);
    }
}
