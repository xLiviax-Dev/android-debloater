using SkiaSharp;

namespace AndroidDebloater.Core.Services;

/// <summary>
/// Dekodiert Android-Icons (PNG und WebP) und wandelt sie in PNG um.
/// WebP kann nicht überall direkt dargestellt werden, deshalb der Umweg über SkiaSharp.
/// </summary>
internal static class SkiaImageDecoder
{
    /// <summary>Wandelt ein kodiertes Bild (PNG/WebP/JPEG) in PNG-Bytes um; null bei Fehlern.</summary>
    public static byte[]? ToPng(byte[] encoded)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(encoded);
            if (bitmap is null)
            {
                return null;
            }

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data?.ToArray();
        }
        catch
        {
            return null;
        }
    }
}
