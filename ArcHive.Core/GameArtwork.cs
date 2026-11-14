using System.Text.Json;
using System.Text.RegularExpressions;

namespace ArcHive.Core;

public enum ArtworkKind { Cover, Backdrop, Screenshot }
public enum ControllerSupport { Unknown, None, Partial, Full }
public sealed record ArtworkReference(ArtworkKind Kind, string Url, string Provider, string ProviderId, DateTimeOffset RetrievedAt);
public sealed class GameArtwork
{
    public ArtworkReference? Cover { get; set; }
    public ArtworkReference? Backdrop { get; set; }
    public List<ArtworkReference> Screenshots { get; set; } = [];
}
public static class ProviderArtwork
{
    // Exact provider CDN hosts only; imported metadata cannot point the downloader at arbitrary hosts.
    private static readonly Dictionary<string, string[]> Hosts = new()
    {
        ["igdb"] = ["images.igdb.com"], ["rawg"] = ["media.rawg.io"],
        ["itch"] = ["img.itch.zone", "img.itch.io"], ["vndb"] = ["t.vndb.org", "s.vndb.org"]
    };
    public static Uri Validate(ArtworkReference art)
    {
        if (art is null || art.Url is null || !Enum.IsDefined(art.Kind) || string.IsNullOrWhiteSpace(art.ProviderId) || art.ProviderId.Length > 200 || art.RetrievedAt == default || art.Url.Length > 2048 ||
            !Uri.TryCreate(art.Url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
            !Hosts.TryGetValue(art.Provider, out var hosts) || !hosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("Unsupported artwork source. Use an HTTPS image on the provider's approved CDN.");
        return uri;
    }
    public static ArtworkReference[] Parse(string provider, string id, JsonElement row, DateTimeOffset at)
    {
        var result = new List<ArtworkReference>();
        void Add(ArtworkKind kind, string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            if (url.StartsWith("//")) url = "https:" + url;
            // itch's documented historical examples use HTTP; upgrade only to the validated CDN.
            if (provider == "itch" && url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) url = "https://" + url[7..];
            var art = new ArtworkReference(kind, url, provider, id, at);
            try { Validate(art); if (!result.Contains(art) && result.Count < 32) result.Add(art); }
            catch (InvalidDataException) { /* Unsupported CDN URLs do not invalidate text metadata. */ }
        }
        string Text(JsonElement node, string field) => node.TryGetProperty(field, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString()! : "";
        void Images(string field, ArtworkKind kind, string property, Func<string, string>? transform = null)
        {
            if (!row.TryGetProperty(field, out var array) || array.ValueKind != JsonValueKind.Array) return;
            foreach (var item in array.EnumerateArray().Take(24))
            { var value = Text(item, property); if (value.Length > 0) Add(kind, transform is null ? value : transform(value)); }
        }
        string Igdb(string imageId, string size) => Regex.IsMatch(imageId, "^[A-Za-z0-9_]+$") ? $"https://images.igdb.com/igdb/image/upload/t_{size}/{imageId}.jpg" : "";
        switch (provider)
        {
            case "igdb":
                if (row.TryGetProperty("cover", out var cover) && cover.ValueKind == JsonValueKind.Object) Add(ArtworkKind.Cover, Igdb(Text(cover, "image_id"), "cover_big"));
                Images("artworks", ArtworkKind.Backdrop, "image_id", v => Igdb(v, "screenshot_big"));
                Images("screenshots", ArtworkKind.Screenshot, "image_id", v => Igdb(v, "screenshot_big")); break;
            case "rawg":
                Add(ArtworkKind.Backdrop, Text(row, "background_image"));
                Images("short_screenshots", ArtworkKind.Screenshot, "image"); break;
            case "itch": Add(ArtworkKind.Cover, Text(row, "cover_url")); break;
            case "vndb":
                if (row.TryGetProperty("image", out var image) && image.ValueKind == JsonValueKind.Object) Add(ArtworkKind.Cover, Text(image, "url"));
                Images("screenshots", ArtworkKind.Screenshot, "url"); break;
        }
        return result.ToArray();
    }
    public static void Select(Game game, ArtworkReference reference, ArtworkKind role)
    {
        Validate(reference); var art = reference with { Kind = role }; Validate(art);
        switch (role)
        {
            case ArtworkKind.Cover: game.Artwork.Cover = art; break;
            case ArtworkKind.Backdrop: game.Artwork.Backdrop = art; break;
            case ArtworkKind.Screenshot:
                if (game.Artwork.Screenshots.Any(a => a.Url == art.Url)) return;
                if (game.Artwork.Screenshots.Count >= 24) throw new InvalidOperationException("A game can have up to 24 selected screenshots.");
                game.Artwork.Screenshots.Add(art); break;
        }
    }
}
