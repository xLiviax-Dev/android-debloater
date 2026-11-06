using System.Security.Cryptography;

namespace AndroidDebloater.Core.Services;

/// <summary>Berechnet Prüfsummen von Dateien (SHA-1 / SHA-256).</summary>
public static class Checksums
{
    /// <summary>Berechnet SHA-1 einer Datei als Hex-Zeichenkette (Kleinbuchstaben).</summary>
    public static Task<string> ComputeSha1Async(string filePath, CancellationToken cancellationToken = default)
        => ComputeFileAsync(filePath, HashAlgorithmName.SHA1, cancellationToken);

    /// <summary>Berechnet SHA-256 einer Datei als Hex-Zeichenkette (Kleinbuchstaben).</summary>
    public static Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken = default)
        => ComputeFileAsync(filePath, HashAlgorithmName.SHA256, cancellationToken);

    /// <summary>Berechnet die Prüfsumme eines Datenstroms als Hex (Kleinbuchstaben).</summary>
    public static async Task<string> ComputeAsync(
        Stream stream,
        HashAlgorithmName algorithm,
        CancellationToken cancellationToken = default)
    {
        using var hasher = Create(algorithm);
        var hash = await hasher.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }

    private static async Task<string> ComputeFileAsync(
        string filePath,
        HashAlgorithmName algorithm,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        return await ComputeAsync(stream, algorithm, cancellationToken).ConfigureAwait(false);
    }

    private static HashAlgorithm Create(HashAlgorithmName algorithm)
        => algorithm == HashAlgorithmName.SHA1 ? SHA1.Create()
         : algorithm == HashAlgorithmName.SHA256 ? SHA256.Create()
         : throw new NotSupportedException($"Nicht unterstuetzter Algorithmus: {algorithm.Name}");
}
