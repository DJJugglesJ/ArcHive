using System.Security.Cryptography;
using System.Text;

namespace ArcHive.Core;

public sealed class ArtworkCache
{
    private readonly string root;
    private readonly HttpClient client;
    private readonly Func<byte[], CancellationToken, Task> validateImage;
    private readonly long maxBytes;
    private readonly int maxImageBytes;
    // Serializes disk publication/eviction and deduplicates concurrent requests for the same URL.
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, DateTimeOffset> failures = [];
    public ArtworkCache(string directory, HttpClient client, Func<byte[], CancellationToken, Task> validateImage,
        long maxBytes = 256 * 1024 * 1024, int maxImageBytes = 12 * 1024 * 1024)
    {
        if (maxBytes < maxImageBytes || maxImageBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxBytes));
        root = Path.GetFullPath(directory); this.client = client; this.validateImage = validateImage;
        this.maxBytes = maxBytes; this.maxImageBytes = maxImageBytes;
    }
    public async Task<byte[]> GetAsync(ArtworkReference reference, CancellationToken token)
    {
        var uri = ProviderArtwork.Validate(reference);
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri))).ToLowerInvariant();
        await gate.WaitAsync(token);
        string? temporary = null;
        try
        {
            Directory.CreateDirectory(root); PathSafety.NoLinks(root);
            var path = Path.Combine(root, key + ".image"); PathSafety.NoLinks(path);
            if (File.Exists(path))
            {
                try
                {
                    if (new FileInfo(path).Length > maxImageBytes) throw new InvalidDataException();
                    var cached = await File.ReadAllBytesAsync(path, token); await validateImage(cached, token);
                    File.SetLastWriteTimeUtc(path, DateTime.UtcNow); Trim(0, path); return cached;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or ArgumentException)
                { File.Delete(path); }
            }
            if (failures.TryGetValue(key, out var until) && until > DateTimeOffset.UtcNow) throw new IOException("Artwork is temporarily unavailable. Retry later.");
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(20));
            // No provider credentials or shared authenticated headers are used for image downloads.
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (!response.IsSuccessStatusCode) throw new IOException("Artwork download failed.");
            var media = response.Content.Headers.ContentType?.MediaType;
            if (media is not ("image/png" or "image/jpeg")) throw new InvalidDataException("Artwork must be PNG or JPEG.");
            if (response.Content.Headers.ContentLength > maxImageBytes) throw new InvalidDataException("Artwork is too large.");
            using var stream = await response.Content.ReadAsStreamAsync(deadline.Token); using var buffer = new MemoryStream();
            var block = new byte[8192]; int count;
            while ((count = await stream.ReadAsync(block, deadline.Token)) > 0)
            {
                if (buffer.Length + count > maxImageBytes) throw new InvalidDataException("Artwork is too large.");
                buffer.Write(block, 0, count);
            }
            var bytes = buffer.ToArray(); await validateImage(bytes, deadline.Token); deadline.Token.ThrowIfCancellationRequested();
            Trim(bytes.Length, path);
            temporary = Path.Combine(root, key + "." + Guid.NewGuid().ToString("N") + ".tmp");
            await File.WriteAllBytesAsync(temporary, bytes, deadline.Token); deadline.Token.ThrowIfCancellationRequested();
            File.Move(temporary, path); temporary = null; failures.Remove(key); return bytes;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or HttpRequestException or NotSupportedException or ArgumentException)
        {
            if (failures.Count >= 1000) failures.Clear(); failures[key] = DateTimeOffset.UtcNow.AddSeconds(30);
            throw new IOException("Artwork could not be loaded. Existing selections are retained.");
        }
        finally { try { if (temporary is not null && File.Exists(temporary)) File.Delete(temporary); } finally { gate.Release(); } }
    }
    private void Trim(long incoming, string keep)
    {
        var files = new DirectoryInfo(root).EnumerateFiles("*.image").ToArray(); var total = files.Sum(f => f.Length);
        foreach (var file in files.Where(f => f.FullName != keep).OrderBy(f => f.LastWriteTimeUtc))
        {
            if (total + incoming <= maxBytes) break;
            PathSafety.NoLinks(file.FullName); var length = file.Length; file.Delete(); total -= length;
        }
        // Only remove abandoned staging files within this dedicated cache, never user artwork.
        foreach (var file in new DirectoryInfo(root).EnumerateFiles("*.tmp").Where(f => f.LastWriteTimeUtc < DateTime.UtcNow.AddHours(-1)))
        { PathSafety.NoLinks(file.FullName); file.Delete(); }
    }
}
