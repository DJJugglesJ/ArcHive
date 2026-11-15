using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ArcHive.Core;

public sealed class RawgMetadataProvider(ProviderTransport transport, string apiKey) : CachedMetadataProvider, IMetadataDetailsProvider
{
    public override string Id => "rawg";
    protected override async Task<MetadataCandidate[]> FetchAsync(string title, CancellationToken token)
    {
        var key = Uri.EscapeDataString(Required(apiKey));
        using var json = await transport.SendAsync(new(HttpMethod.Get, $"https://api.rawg.io/api/games?key={key}&search={Uri.EscapeDataString(title)}&page_size=20&page=1"), token);
        var now = DateTimeOffset.UtcNow;
        return Rows(json.RootElement, "results", 20).EnumerateArray().Select(row =>
        {
            var fields = new Dictionary<MetadataField, string>(); Year(fields, Text(row, "released"));
            var tags = Names(row, "genres").Concat(Names(row, "tags")).Distinct().ToArray();
            if (tags.Length > 0) fields[MetadataField.Tags] = string.Join(", ", tags);
            if (row.TryGetProperty("tags", out var tagRows) && tagRows.ValueKind == JsonValueKind.Array)
            {
                var slugs = tagRows.EnumerateArray().Select(t => Text(t, "slug")).ToArray();
                if (slugs.Contains("full-controller-support")) fields[MetadataField.ControllerSupport] = "Full";
                else if (slugs.Contains("partial-controller-support")) fields[MetadataField.ControllerSupport] = "Partial";
            }
            return new MetadataCandidate(Id, NumericId(row), Text(row, "name"), [], fields, now) { Artwork = ProviderArtwork.Parse(Id, NumericId(row), row, now) };
        }).ToArray();
    }
    public async Task<MetadataCandidate> GetDetailsAsync(MetadataCandidate candidate, CancellationToken token)
    {
        MetadataText.CheckIdentity(candidate, Id);
        var key = Uri.EscapeDataString(Required(apiKey));
        using var json = await transport.SendAsync(new(HttpMethod.Get, $"https://api.rawg.io/api/games/{candidate.Id}?key={key}"), token);
        var row = json.RootElement;
        if (NumericId(row) != candidate.Id) throw new ProviderException("RAWG returned details for a different game.");
        var fields = new Dictionary<MetadataField,string>(candidate.Fields);
        var description = Text(row,"description_raw"); if (description.Length == 0) description = Text(row,"description");
        if (description.Length > 0) fields[MetadataField.Description] = MetadataText.Plain(description);
        var tags = MetadataText.Tags(Names(row,"genres").Concat(Names(row,"tags")));
        if (tags.Length > 0) fields[MetadataField.Tags] = tags;
        Year(fields, Text(row,"released"));
        var name = Text(row,"name");
        var result = candidate with { Title = name.Length > 0 ? name : candidate.Title, Fields = fields, RetrievedAt = DateTimeOffset.UtcNow };
        MetadataValidation.Validate(result); return result;
    }

}

public sealed class IgdbMetadataProvider(ProviderTransport transport, string clientId, string clientSecret) : CachedMetadataProvider
{
    public override string Id => "igdb";
    private string accessToken = "";
    private DateTimeOffset expires;
    private async Task Authenticate(CancellationToken token)
    {
        using var json = await transport.SendAsync(new(HttpMethod.Post, "https://id.twitch.tv/oauth2/token")
        { Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["client_id"] = Required(clientId), ["client_secret"] = Required(clientSecret), ["grant_type"] = "client_credentials" }) }, token);
        accessToken = Required(Text(json.RootElement, "access_token"));
        var seconds = json.RootElement.GetProperty("expires_in").GetInt32();
        if (seconds <= 0 || !string.Equals(Text(json.RootElement, "token_type"), "bearer", StringComparison.OrdinalIgnoreCase)) throw new ProviderException("Provider returned an invalid access token.");
        // Short sessions need no stored refresh token; renew no later than one hour.
        expires = DateTimeOffset.UtcNow.AddSeconds(Math.Min(seconds, 3600) - Math.Min(30, seconds / 2));
    }
    protected override async Task<MetadataCandidate[]> FetchAsync(string title, CancellationToken token)
    {
        if (DateTimeOffset.UtcNow >= expires) await Authenticate(token);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var escaped = title.Replace("\\", "\\\\").Replace("\"", "\\\"");
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.igdb.com/v4/games")
            { Content = new StringContent($"search \"{escaped}\"; fields name,alternative_names.name,summary,first_release_date,game_engines.name,genres.name,themes.name,keywords.name,cover.image_id,artworks.image_id,screenshots.image_id; limit 20;", Encoding.UTF8, "text/plain") };
            request.Headers.Add("Client-ID", Required(clientId)); request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            try
            {
                using var json = await transport.SendAsync(request, token); var rows = json.RootElement;
                if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() > 20) throw new ProviderException("IGDB returned an invalid result count.");
                var now = DateTimeOffset.UtcNow;
                return rows.EnumerateArray().Select(row =>
                {
                    var fields = new Dictionary<MetadataField, string>();
                    if (Text(row, "summary") is { Length: > 0 } description) fields[MetadataField.Description] = MetadataText.Plain(description);
                    if (row.TryGetProperty("first_release_date", out var release) && release.ValueKind == JsonValueKind.Number)
                        fields[MetadataField.ReleaseYear] = DateTimeOffset.FromUnixTimeSeconds(release.GetInt64()).Year.ToString();
                    var engines = Names(row, "game_engines"); if (engines.Length > 0) fields[MetadataField.Engine] = string.Join(", ", engines);
                    var genres = Names(row, "genres").Concat(Names(row, "themes")).Concat(Names(row, "keywords"));
                    var tags = MetadataText.Tags(genres); if (tags.Length > 0) fields[MetadataField.Tags] = tags;
                    return new MetadataCandidate(Id, NumericId(row), Text(row, "name"), Names(row, "alternative_names"), fields, now) { Artwork = ProviderArtwork.Parse(Id, NumericId(row), row, now) };
                }).ToArray();
            }
            catch (ProviderException ex) when (ex.Status == HttpStatusCode.Unauthorized && attempt == 0)
            { expires = default; await Authenticate(token); }
        }
        throw new ProviderException("IGDB authentication failed.");
    }
}

public sealed class SteamMetadataProvider(ProviderTransport transport, string apiKey) : CachedMetadataProvider
{
    public override string Id => "steam";
    private MetadataCandidate[]? catalog;
    private DateTimeOffset catalogAt;
    protected override async Task<MetadataCandidate[]> FetchAsync(string title, CancellationToken token)
    {
        if (catalog is not null && DateTimeOffset.UtcNow - catalogAt < TimeSpan.FromHours(6)) return Best(title, catalog);
        var key = Uri.EscapeDataString(Required(apiKey)); var items = new List<MetadataCandidate>(); long cursor = 0;
        for (var page = 0; page < 50; page++)
        {
            using var json = await transport.SendAsync(new(HttpMethod.Get, $"https://api.steampowered.com/IStoreService/GetAppList/v1/?key={key}&include_games=true&include_dlc=false&include_software=false&include_videos=false&include_hardware=false&max_results=10000&last_appid={cursor}"), token);
            var response = json.RootElement.GetProperty("response"); var rows = Rows(response, "apps", 10000); var now = DateTimeOffset.UtcNow;
            long last = cursor;
            foreach (var row in rows.EnumerateArray())
            {
                var id = NumericId(row, "appid"); var number = long.Parse(id);
                if (number <= last) throw new ProviderException("Steam catalog pagination did not advance."); last = number;
                // The official catalog returns names, not descriptions or release dates.
                if (Text(row, "name") is { Length: > 0 } name) items.Add(new(Id, id, name, [], [], now));
            }
            if (!response.TryGetProperty("have_more_results", out var more) || !more.GetBoolean())
            { var complete = items.ToArray(); var best = Best(title, complete); catalog = complete; catalogAt = now; return best; }
            if (last <= cursor) throw new ProviderException("Steam catalog pagination did not advance.");
            cursor = last;
        }
        throw new ProviderException("Steam catalog exceeded the 500,000-entry limit. No partial catalog was cached.");
    }
}

public sealed class ItchMetadataProvider(ProviderTransport transport, string apiKey, bool projects = false) : CachedMetadataProvider
{
    public override string Id => "itch";
    private MetadataCandidate[]? catalog;
    private DateTimeOffset catalogAt;
    protected override async Task<MetadataCandidate[]> FetchAsync(string title, CancellationToken token)
    {
        if (catalog is not null && DateTimeOffset.UtcNow - catalogAt < TimeSpan.FromMinutes(10)) return Best(title, catalog);
        var key = Required(apiKey); var items = new Dictionary<string, MetadataCandidate>(); var seenKeys = new HashSet<string>();
        for (var page = 1; page <= 100; page++)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, projects ? "https://api.itch.io/profile/games" : $"https://api.itch.io/profile/owned-keys?page={page}&per_page=500");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            using var json = await transport.SendAsync(request, token); var root = json.RootElement;
            if (root.TryGetProperty("errors", out _)) throw new ProviderException("itch.io rejected the request. Check credentials and scopes.");
            // The live owned-key endpoint represents its empty terminal page as {}.
            // Accept only an empty object for this field, not malformed nonempty objects.
            if (!projects && root.TryGetProperty("owned_keys", out var terminal) && terminal.ValueKind == JsonValueKind.Object && !terminal.EnumerateObject().Any())
            { var complete = items.Values.ToArray(); var best = Best(title, complete); catalog = complete; catalogAt = DateTimeOffset.UtcNow; return best; }
            var rows = Rows(root, projects ? "games" : "owned_keys", projects ? 10000 : 500); var now = DateTimeOffset.UtcNow;
            foreach (var entry in rows.EnumerateArray())
            {
                if (!projects && !seenKeys.Add(NumericId(entry))) throw new ProviderException("itch.io pagination repeated an owned key. Retry after the account stops changing.");
                var row = projects ? entry : entry.GetProperty("game");
                if (Text(row, "classification") is { Length: > 0 } classification && classification != "game") continue;
                var id = NumericId(row); var fields = new Dictionary<MetadataField, string>();
                if (Text(row, "short_text") is { Length: > 0 } description) fields[MetadataField.Description] = MetadataText.Plain(description);
                // published_at is the itch page publication date, not a game release date.
                items[id] = new(Id, id, Text(row, "title"), [], fields, now) { Artwork = ProviderArtwork.Parse(Id, id, row, now) };
            }
            // Owned-key pages may be short before the end. Only an empty page terminates.
            if (projects || rows.GetArrayLength() == 0)
            { var complete = items.Values.ToArray(); var best = Best(title, complete); catalog = complete; catalogAt = now; return best; }
        }
        throw new ProviderException("itch.io library exceeded the 50,000-key limit. No partial library was cached.");
    }
}
