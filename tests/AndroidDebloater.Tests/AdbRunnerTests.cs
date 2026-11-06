using AndroidDebloater.Core.Services;

namespace AndroidDebloater.Tests;

public class AdbRunnerTests
{
    [Theory]
    [InlineData("Android Debug Bridge version 1.0.41\nVersion 37.0.1-14910801\nInstalled as C:\\platform-tools\\adb.exe", "37.0.1")]
    [InlineData("Version 35.0.2-1234567", "35.0.2")]
    [InlineData("Version 37.0.1", "37.0.1")]
    public void ParseVersion_ReadsPlatformToolsVersion(string output, string expected)
    {
        Assert.Equal(expected, AdbRunner.ParseVersion(output));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Android Debug Bridge version 1.0.41")]
    public void ParseVersion_ReturnsNull_WhenNoVersionLine(string output)
    {
        Assert.Null(AdbRunner.ParseVersion(output));
    }
}
