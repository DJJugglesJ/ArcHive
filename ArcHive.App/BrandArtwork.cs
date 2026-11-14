using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ArcHive.App;

public static class BrandArtwork
{
    // Screen-composite the original black-backed artwork onto the UI surface.
    // RGB bytes stay unchanged in premultiplied space. Alpha=max(R,G,B)
    // makes black transparent without a hard color-key edge or redrawing the mark.
    public static BitmapSource Logo { get; } = CreateLogo();
    private static BitmapSource CreateLogo()
    {
        var original = new BitmapImage(new Uri("pack://application:,,,/Assets/ArcHive-brand-approved.png"));
        var crop = new CroppedBitmap(original, new Int32Rect(134, 150, 680, 710));
        var source = new FormatConvertedBitmap(crop, PixelFormats.Bgra32, null, 0);
        var stride = source.PixelWidth * 4;
        var pixels = new byte[stride * source.PixelHeight];
        source.CopyPixels(pixels, stride, 0);
        for (var i = 0; i < pixels.Length; i += 4)
            pixels[i + 3] = Math.Max(pixels[i], Math.Max(pixels[i + 1], pixels[i + 2]));
        var result = BitmapSource.Create(source.PixelWidth, source.PixelHeight, 96, 96,
            PixelFormats.Pbgra32, null, pixels, stride);
        result.Freeze();
        return result;
    }
}
