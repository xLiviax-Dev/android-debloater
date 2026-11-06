using AndroidDebloater.Core.Localization;

namespace AndroidDebloater.Tests;

public class LocalizationTests
{
    [Fact]
    public void AppTitle_IsResolvedFromResource()
    {
        Assert.Equal("Android Debloater", Strings.AppTitle);
    }

    [Fact]
    public void ShellStrings_AreResolvedFromResource()
    {
        Assert.NotEqual(nameof(Strings.ShellHint), Strings.ShellHint);
        Assert.NotEqual(nameof(Strings.StatusReady), Strings.StatusReady);
    }
}
