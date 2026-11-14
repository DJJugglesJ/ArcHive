using System.IO;
using System.Windows.Media.Imaging;
using ArcHive.Core;

namespace ArcHive.App;

public static class ArtworkImages
{
    public static BitmapSource Decode(byte[] bytes, bool allowBmp = false)
    {
        var dimensions = allowBmp && bytes.Length >= 54 && bytes[0] == 66 && bytes[1] == 77
            ? (Width: BitConverter.ToInt32(bytes, 18), Height: BitConverter.ToInt32(bytes, 22)) : ArtworkImage.Dimensions(bytes);
        if (dimensions.Height == int.MinValue) throw new InvalidDataException("Invalid bitmap dimensions.");
        dimensions.Height = Math.Abs(dimensions.Height);
        if (dimensions.Width <= 0 || dimensions.Height <= 0 || dimensions.Width > 8192 || dimensions.Height > 8192 || (long)dimensions.Width * dimensions.Height > 32_000_000) throw new InvalidDataException("Artwork dimensions exceed limits.");
        try
        {
            using var stream = new MemoryStream(bytes, false);
            var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = Math.Max(1, (int)(dimensions.Width * Math.Min(1d, 1280d / Math.Max(dimensions.Width, dimensions.Height)))); bitmap.StreamSource = stream; bitmap.EndInit();
            // Force decoding before a download is published to disk.
            var stride = checked((bitmap.PixelWidth * bitmap.Format.BitsPerPixel + 7) / 8);
            bitmap.CopyPixels(new byte[checked(stride * bitmap.PixelHeight)], stride, 0);
            bitmap.Freeze(); return bitmap;
        }
        catch (Exception ex) when (ex is IOException or FileFormatException or NotSupportedException or ArgumentException or System.Runtime.InteropServices.COMException)
        { throw new InvalidDataException("Artwork could not be decoded."); }
    }
    public static Task Validate(byte[] bytes, CancellationToken token) => Task.Run(() => { token.ThrowIfCancellationRequested(); Decode(bytes); token.ThrowIfCancellationRequested(); }, token);
}
