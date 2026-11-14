using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ArcHive.Core;

// Caller owns HttpClient. No credentials, artwork, local paths or background requests.
public sealed class VndbMetadataProvider(HttpClient client) : IMetadataProvider
{
    public string Id => "vndb";
    private readonly SemaphoreSlim gate = new(1, 1);
    private DateTimeOffset nextRequest;
    private readonly Dictionary<string, (DateTimeOffset Time, MetadataCandidate[] Items)> cache = new();
    public async Task<IReadOnlyList<MetadataCandidate>> SearchAsync(MetadataQuery query, CancellationToken cancellationToken)
    {
        var title = query.Title.Trim();
        if (title.Length is < 2 or > 200) throw new ArgumentException("Search titles must contain 2–200 characters.");
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue(title, out var cached) && DateTimeOffset.UtcNow - cached.Time < TimeSpan.FromMinutes(10))
                return Clone(cached.Items);
            if (DateTimeOffset.UtcNow < nextRequest) throw new InvalidOperationException("VNDB is cooling down. Please try again later.");
            nextRequest = DateTimeOffset.UtcNow.AddSeconds(2);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(20));
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.vndb.org/kana/vn")
            {
                Content = JsonContent.Create(new { filters = new[] { "search", "=", title },
                    fields = "title,alttitle,aliases,description,released,image.url,screenshots.url", sort = "searchrank", results = 20 })
            };
            request.Headers.UserAgent.ParseAdd("ArcHive/0.9.0");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var retry = response.Headers.RetryAfter?.Delta ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow) ?? TimeSpan.FromMinutes(1);
                nextRequest = DateTimeOffset.UtcNow.AddSeconds(Math.Clamp(retry.TotalSeconds, 2, 3600));
                throw new HttpRequestException("VNDB rate limit reached. Wait before retrying.", null, response.StatusCode);
            }
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var buffer = new MemoryStream(); var block = new byte[8192]; int count;
            while ((count = await stream.ReadAsync(block, deadline.Token)) > 0)
            {
                if (buffer.Length + count > 2_000_000) throw new InvalidDataException("VNDB response exceeds 2 MB.");
                buffer.Write(block, 0, count);
            }
            using var json = JsonDocument.Parse(buffer.ToArray());
            var rows = json.RootElement.GetProperty("results");
            if (rows.GetArrayLength() > 20) throw new InvalidDataException("VNDB returned too many candidates.");
            var now = DateTimeOffset.UtcNow;
            var items = rows.EnumerateArray().Select(row =>
            {
                string Text(string name) => row.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString()! : "";
                var id = Text("id");
                if (!System.Text.RegularExpressions.Regex.IsMatch(id, @"^v[1-9][0-9]*$")) throw new InvalidDataException("Invalid VNDB identifier.");
                var fields = new Dictionary<MetadataField, string> { [MetadataField.Description] = Text("description") };
                var released = Text("released");
                if (released.Length >= 4 && int.TryParse(released[..4], out var year) && year is >= 1000 and <= 9999) fields[MetadataField.ReleaseYear] = year.ToString();
                var aliases = row.TryGetProperty("aliases", out var a) && a.ValueKind == JsonValueKind.Array ? a.EnumerateArray().Select(x => x.GetString() ?? "").ToList() : [];
                if (Text("alttitle").Length > 0) aliases.Add(Text("alttitle"));
                var candidate = new MetadataCandidate(Id, id, Text("title"), aliases.ToArray(), fields, now) { Artwork = ProviderArtwork.Parse(Id, id, row, now) };
                MetadataValidation.Validate(candidate); return candidate;
            }).ToArray();
            if (cache.Count >= 100) cache.Clear();
            cache[title] = (now, items);
            return Clone(items);
        }
        finally { gate.Release(); }
    }
    private static MetadataCandidate[] Clone(MetadataCandidate[] items) => items.Select(c => c with { Aliases = c.Aliases.ToArray(), Fields = new(c.Fields), Artwork = c.Artwork.ToArray() }).ToArray();
}
