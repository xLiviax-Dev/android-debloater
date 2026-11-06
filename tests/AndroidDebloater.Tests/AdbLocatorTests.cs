using AndroidDebloater.Core.Models;
using AndroidDebloater.Core.Services;

namespace AndroidDebloater.Tests;

public class AdbLocatorTests
{
    [Fact]
    public void EnumerateCandidates_ContainsPortableAndAppManagedSources()
    {
        var candidates = AdbLocator.EnumerateCandidates().ToList();

        Assert.Contains(candidates, c => c.Source == AdbSource.Portable);
        Assert.Contains(candidates, c => c.Source == AdbSource.AppManaged);
    }

    [Fact]
    public void EnumerateCandidates_ContainsPathEntries_WhenPathIsSet()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PATH")))
        {
            return;
        }

        var candidates = AdbLocator.EnumerateCandidates().ToList();

        Assert.Contains(candidates, c => c.Source == AdbSource.Path);
    }

    [Fact]
    public void EnumerateCandidates_AppManagedPointsToManagedAdb()
    {
        var appManaged = AdbLocator.EnumerateCandidates()
            .First(c => c.Source == AdbSource.AppManaged);

        Assert.Equal(Path.GetFullPath(AppPaths.ManagedAdbPath), appManaged.Path);
    }

    [Fact]
    public void EnumerateCandidates_ReturnsDistinctPaths()
    {
        var comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        var paths = AdbLocator.EnumerateCandidates().Select(c => c.Path).ToList();

        Assert.Equal(paths.Count, paths.Distinct(comparer).Count());
    }

    [Fact]
    public void FindAdb_WhenFound_PointsToExistingFile()
    {
        var found = AdbLocator.FindAdb();

        if (found is not null)
        {
            Assert.True(File.Exists(found.Value.Path));
        }
    }
}
