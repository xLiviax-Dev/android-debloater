using AndroidDebloater.Core.Models;
using AndroidDebloater.Core.Services;

namespace AndroidDebloater.Tests;

public class PlatformToolsManifestReaderTests
{
    private const string SampleManifest = """
        <?xml version="1.0" encoding="UTF-8"?>
        <sdk:sdk-repository xmlns:sdk="http://schemas.android.com/repository/android/generic/01">
          <remotePackage path="platform-tools">
            <revision>
              <major>37</major>
              <minor>0</minor>
              <micro>1</micro>
            </revision>
            <archives>
              <archive>
                <complete>
                  <size>8044989</size>
                  <checksum>E03E78B1D80B396F1C3358E31251CB31740E1110</checksum>
                  <url>platform-tools_r37.0.1-win.zip</url>
                </complete>
                <host-os>windows</host-os>
              </archive>
              <archive>
                <complete>
                  <size>16110554</size>
                  <checksum>6ae73f4de6452dc57e62ec02b68eed92a4c21661</checksum>
                  <url>platform-tools_r37.0.1-darwin.zip</url>
                </complete>
                <host-os>macosx</host-os>
              </archive>
              <archive>
                <complete>
                  <size>9054187</size>
                  <checksum>477254aa5f903c15cf51001717bdf347fb6b53e0</checksum>
                  <url>platform-tools_r37.0.1-linux.zip</url>
                </complete>
                <host-os>linux</host-os>
              </archive>
            </archives>
          </remotePackage>
        </sdk:sdk-repository>
        """;

    [Fact]
    public void Parse_ReadsVersion()
    {
        var release = PlatformToolsManifestReader.Parse(SampleManifest);

        Assert.Equal("37.0.1", release.Version);
    }

    [Fact]
    public void Parse_ReadsAllThreeArchives()
    {
        var release = PlatformToolsManifestReader.Parse(SampleManifest);

        Assert.Equal(3, release.Archives.Count);
    }

    [Fact]
    public void Parse_ReadsWindowsArchiveDetails_AndLowercasesChecksum()
    {
        var release = PlatformToolsManifestReader.Parse(SampleManifest);

        var windows = release.ForHost(HostOperatingSystem.Windows);

        Assert.NotNull(windows);
        Assert.Equal("platform-tools_r37.0.1-win.zip", windows!.FileName);
        Assert.Equal(8044989, windows.SizeBytes);
        Assert.Equal("e03e78b1d80b396f1c3358e31251cb31740e1110", windows.Sha1);
    }

    [Fact]
    public void Parse_MapsHostOsTokens()
    {
        var release = PlatformToolsManifestReader.Parse(SampleManifest);

        Assert.NotNull(release.ForHost(HostOperatingSystem.MacOs));
        Assert.NotNull(release.ForHost(HostOperatingSystem.Linux));
    }

    [Fact]
    public void Parse_Throws_WhenPackageMissing()
    {
        const string manifest = "<sdk:sdk-repository xmlns:sdk=\"http://example\" />";

        Assert.Throws<InvalidDataException>(() => PlatformToolsManifestReader.Parse(manifest));
    }

    [Fact]
    public void SelectArchive_PicksArchiveForCurrentHost()
    {
        var release = PlatformToolsManifestReader.Parse(SampleManifest);

        var archive = AdbInstaller.SelectArchive(release);

        Assert.Equal(HostOperatingSystemExtensions.Current, archive.HostOs);
    }
}
