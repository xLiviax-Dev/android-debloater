using AndroidDebloater.Core.Services;

namespace AndroidDebloater.Tests;

/// <summary>
/// Tests der lokalen Spiele-Liste. Das Gerät meldet über "dumpsys game" nur
/// installierte Spiele; die Liste schreibt erkannte Spiele fort, damit entfernte
/// Spiele (Tab "Removed") weiter als Spiel gelten.
/// </summary>
public class GameRegistryTests : IDisposable
{
    private readonly string _file = Path.Combine(
        Path.GetTempPath(),
        $"adb-games-{Guid.NewGuid():N}.txt");

    [Fact]
    public void Load_ReturnsEmpty_WhenFileIsMissing()
        => Assert.Empty(GameRegistry.Load(_file));

    [Fact]
    public void Merge_KeepsKnownGames_WhenDeviceReportsNothing()
    {
        GameRegistry.Merge(new[] { "com.games.layerman" }, _file);

        var merged = GameRegistry.Merge(Array.Empty<string>(), _file);

        Assert.Equal(new[] { "com.games.layerman" }, merged);
    }

    [Fact]
    public void Merge_AddsNewGamesAndKeepsOldOnes()
    {
        GameRegistry.Merge(new[] { "com.games.layerman" }, _file);

        var merged = GameRegistry.Merge(new[] { "com.mojang.minecraftpe" }, _file);

        Assert.Equal(2, merged.Count);
        Assert.Contains("com.games.layerman", merged);
        Assert.Contains("com.mojang.minecraftpe", merged);
        Assert.Contains("com.mojang.minecraftpe", GameRegistry.Load(_file));
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_file))
            {
                File.Delete(_file);
            }
        }
        catch
        {
            // Aufräumen darf den Testlauf nicht stören.
        }

        GC.SuppressFinalize(this);
    }
}
