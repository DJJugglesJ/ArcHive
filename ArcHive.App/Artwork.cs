using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using ArcHive.Core;

namespace ArcHive.App;

// Recycled containers cancel work and cannot display an earlier request.
public sealed class Artwork : Image
{
    public static readonly DependencyProperty ImagePathProperty = DependencyProperty.Register(nameof(ImagePath), typeof(string), typeof(Artwork), new PropertyMetadata(null, Changed));
    public static readonly DependencyProperty ReferenceProperty = DependencyProperty.Register(nameof(Reference), typeof(ArtworkReference), typeof(Artwork), new PropertyMetadata(null, Changed));
    public string? ImagePath { get => (string?)GetValue(ImagePathProperty); set => SetValue(ImagePathProperty, value); }
    public ArtworkReference? Reference { get => (ArtworkReference?)GetValue(ReferenceProperty); set => SetValue(ReferenceProperty, value); }
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
    private static readonly ArtworkCache Downloads = new(Path.Combine(Environment.GetEnvironmentVariable("ARCHIVE_DATA_DIRECTORY") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArcHive"), "artwork-cache"), Client, ArtworkImages.Validate);
    private static readonly SemaphoreSlim Decoders = new(2);
    private static readonly Dictionary<string, BitmapSource> Memory = new();
    private CancellationTokenSource? loading;
    private int generation;
    public Artwork()
    {
        Loaded += (_, _) => LoadImage();
        Unloaded += (_, _) => { generation++; loading?.Cancel(); Source = null; };
    }
    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    { if (((Artwork)sender).IsLoaded) ((Artwork)sender).LoadImage(); }
    private async void LoadImage()
    {
        var request = ++generation; loading?.Cancel();
        using var source = new CancellationTokenSource(); loading = source;
        Source = null; var path = ImagePath; var reference = Reference;
        var key = reference?.Url ?? path;
        if (string.IsNullOrEmpty(key)) { loading = null; return; }
        if (Memory.TryGetValue(key, out var cached)) { Source = cached; loading = null; return; }
        try
        {
            var remoteLoaded = false;
            var result = await Task.Run(async () =>
            {
                byte[] bytes;
                try
                {
                    if (reference is null) throw new IOException();
                    bytes = await Downloads.GetAsync(reference, source.Token); remoteLoaded = true;
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
                {
                    if (string.IsNullOrEmpty(path)) return null;
                    if (new FileInfo(path).Length > 25 * 1024 * 1024) return null;
                    bytes = await File.ReadAllBytesAsync(path, source.Token);
                }
                await Decoders.WaitAsync(source.Token);
                try { source.Token.ThrowIfCancellationRequested(); return ArtworkImages.Decode(bytes, allowBmp: !remoteLoaded); }
                finally { Decoders.Release(); }
            }, source.Token);
            if (request != generation || !IsLoaded || result is null) return;
            // Local fallback is not cached under a failed remote URL, so a retry can recover.
            if (Memory.Count >= 32 || Memory.Values.Sum(b => (long)b.PixelWidth * b.PixelHeight * 4) + (long)result.PixelWidth * result.PixelHeight * 4 > 64 * 1024 * 1024) Memory.Clear();
            if (reference is null || remoteLoaded) Memory[key] = result;
            Source = result;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or OperationCanceledException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        { /* Missing/offline/invalid artwork retains the neutral placeholder. */ }
        finally { if (ReferenceEquals(loading, source)) loading = null; }
    }
}
