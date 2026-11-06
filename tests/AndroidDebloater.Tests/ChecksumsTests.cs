using System.Text;
using AndroidDebloater.Core.Services;

namespace AndroidDebloater.Tests;

public class ChecksumsTests
{
    [Fact]
    public async Task ComputeSha1_MatchesKnownValue()
    {
        // Bekannter Referenzwert: SHA-1("abc").
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("abc"));

        var hash = await Checksums.ComputeAsync(stream, System.Security.Cryptography.HashAlgorithmName.SHA1);

        Assert.Equal("a9993e364706816aba3e25717850c26c9cd0d89d", hash);
    }

    [Fact]
    public async Task ComputeSha256_MatchesKnownValue()
    {
        // Bekannter Referenzwert: SHA-256("abc").
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("abc"));

        var hash = await Checksums.ComputeAsync(stream, System.Security.Cryptography.HashAlgorithmName.SHA256);

        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash);
    }

    [Fact]
    public async Task ComputeSha1Async_ReadsFile()
    {
        var path = Path.Combine(Path.GetTempPath(), "AndroidDebloater-test-" + Guid.NewGuid().ToString("N") + ".bin");
        await File.WriteAllTextAsync(path, "abc", Encoding.ASCII);
        try
        {
            var hash = await Checksums.ComputeSha1Async(path);
            Assert.Equal("a9993e364706816aba3e25717850c26c9cd0d89d", hash);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
