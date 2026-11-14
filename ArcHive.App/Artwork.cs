using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace ArcHive.App;

// Decode off the UI thread; recycled containers cannot display an earlier request.
public sealed class Artwork : Image
{
    public static readonly DependencyProperty ImagePathProperty = DependencyProperty.Register(
        nameof(ImagePath), typeof(string), typeof(Artwork), new PropertyMetadata(null, Changed));
    public string? ImagePath { get => (string?)GetValue(ImagePathProperty); set => SetValue(ImagePathProperty, value); }
    private static readonly SemaphoreSlim Decoders = new(2);
    private static readonly Dictionary<string, BitmapSource> Cache = new();
    private int generation;
    public Artwork()
    {
        Loaded += (_, _) => LoadImage();
        Unloaded += (_, _) => { generation++; Source = null; };
    }
    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    { if (((Artwork)sender).IsLoaded) ((Artwork)sender).LoadImage(); }
    private async void LoadImage()
    {
        var request = ++generation;
        Source = null;
        var path = ImagePath;
        if (string.IsNullOrEmpty(path)) return;
        if (Cache.TryGetValue(path, out var cached)) { Source = cached; return; }
        try
        {
            var result = await Task.Run(async () =>
            {
                await Decoders.WaitAsync();
                try
                {
                    if (request != Volatile.Read(ref generation)) return null;
                    using var stream = File.OpenRead(path);
                    if (stream.Length > 25 * 1024 * 1024) return null;
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = 640; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
                    return bitmap;
                }
                finally { Decoders.Release(); }
            });
            if (request != generation || !IsLoaded || result is null) return;
            if (Cache.Count >= 64) Cache.Clear();
            Cache[path] = result;
            Source = result;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or ArgumentException or System.Runtime.InteropServices.COMException)
        { /* A missing or invalid image retains the neutral artwork placeholder. */ }
    }
}
