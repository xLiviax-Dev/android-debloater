using System.Text.RegularExpressions;

namespace AndroidDebloater.Core.Services;

/// <summary>Größen eines Pakets laut "dumpsys diskstats" (Angaben in Bytes).</summary>
/// <param name="ApkBytes">Größe der APK(s).</param>
/// <param name="DataBytes">Größe der App-Daten.</param>
/// <param name="CacheBytes">Größe des Caches.</param>
public readonly record struct PackageSizes(long ApkBytes, long DataBytes, long CacheBytes);

/// <summary>
/// Zerlegt die Ausgabe von "adb shell dumpsys diskstats". Die App-, Daten-
/// und Cache-Größen liegen dort als drei gleich lange Listen vor, die über
/// den Index zur Paketnamen-Liste passen.
/// </summary>
public static class DiskStatsParser
{
    private const string NamesKey = "Package Names";
    private const string ApkKey = "App Sizes";
    private const string DataKey = "App Data Sizes";
    private const string CacheKey = "Cache Sizes";

    /// <summary>Liest die Größen je Paketname.</summary>
    public static IReadOnlyDictionary<string, PackageSizes> Parse(string dump)
    {
        var result = new Dictionary<string, PackageSizes>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(dump))
        {
            return result;
        }

        var names = ExtractList(dump, NamesKey);
        if (names.Count == 0)
        {
            return result;
        }

        var apk = ExtractList(dump, ApkKey);
        var data = ExtractList(dump, DataKey);
        var cache = ExtractList(dump, CacheKey);

        for (var i = 0; i < names.Count; i++)
        {
            var name = names[i].Trim().Trim('"');
            if (name.Length == 0)
            {
                continue;
            }

            result[name] = new PackageSizes(
                ParseLong(apk, i),
                ParseLong(data, i),
                ParseLong(cache, i));
        }

        return result;
    }

    /// <summary>Liest die Liste hinter "&lt;key&gt;: [ ... ]".</summary>
    private static List<string> ExtractList(string text, string key)
    {
        var match = Regex.Match(
            text,
            Regex.Escape(key) + @":\s*\[(.*?)\]",
            RegexOptions.Singleline);

        if (!match.Success)
        {
            return new List<string>();
        }

        return match.Groups[1].Value
            .Split(',', StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static long ParseLong(IReadOnlyList<string> values, int index)
        => index < values.Count && long.TryParse(values[index].Trim().Trim('"'), out var value)
            ? value
            : 0;
}
