using AndroidDebloater.Core.Models;
using AndroidDebloater.Core.Services;

namespace AndroidDebloater.Tests;

/// <summary>
/// Tests der Nutzereinstellungen (projektinfo.txt M8): Laden/Speichern als JSON,
/// fehlertolerantes Lesen und Anwenden auf die Laufzeit-Schutzlisten.
/// </summary>
public class SettingsStoreTests : IDisposable
{
    private readonly string _folder =
        Path.Combine(Path.GetTempPath(), "adbdebloater-tests-" + Guid.NewGuid().ToString("N"));

    public SettingsStoreTests() => ProtectionLists.Reset();

    public void Dispose() => ProtectionLists.Reset();

    private string PathFor(string name) => Path.Combine(_folder, name);

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var settings = SettingsStore.Load(PathFor("missing.json"));

        Assert.False(settings.ExpertMode);
        Assert.Equal(AppSettings.DefaultLanguage, settings.Language);
        Assert.Null(settings.AdbPath);
        Assert.Null(settings.Whitelist);
    }

    [Fact]
    public void Load_InvalidJson_ReturnsDefaults()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(PathFor("broken.json"), "{ this is not json");

        var settings = SettingsStore.Load(PathFor("broken.json"));

        Assert.False(settings.ExpertMode);
        Assert.Equal(AppSettings.DefaultLanguage, settings.Language);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsAllValues()
    {
        var path = PathFor("settings.json");
        var original = new AppSettings
        {
            ExpertMode = true,
            Language = "en",
            AdbPath = @"C:\tools\adb.exe",
            Whitelist = new List<string> { "com.example.one", "com.example.two" },
        };

        await SettingsStore.SaveAsync(original, path);
        var loaded = SettingsStore.Load(path);

        Assert.True(loaded.ExpertMode);
        Assert.Equal("en", loaded.Language);
        Assert.Equal(@"C:\tools\adb.exe", loaded.AdbPath);
        Assert.Equal(new[] { "com.example.one", "com.example.two" }, loaded.Whitelist);
    }

    [Fact]
    public void EffectiveWhitelist_WithoutCustomSet_UsesDefaults()
    {
        var settings = new AppSettings();

        Assert.Equal(
            ProtectionLists.DefaultWhitelist.OrderBy(entry => entry, StringComparer.Ordinal),
            settings.EffectiveWhitelist.OrderBy(entry => entry, StringComparer.Ordinal));
    }

    [Fact]
    public void Apply_SetsWhitelistAndExpertMode()
    {
        var settings = new AppSettings
        {
            ExpertMode = true,
            Whitelist = new List<string> { "com.example.keep" },
        };

        SettingsStore.Apply(settings);

        Assert.True(ProtectionLists.ExpertModeEnabled);
        Assert.True(ProtectionLists.IsWhitelisted("com.example.keep"));
        Assert.False(ProtectionLists.IsWhitelisted("com.android.vending"));
    }

    [Fact]
    public void SupportedLanguages_ContainsDefaultLanguage()
        => Assert.Contains(AppSettings.DefaultLanguage, SettingsStore.SupportedLanguages);
}
