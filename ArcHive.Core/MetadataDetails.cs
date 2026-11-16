using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace ArcHive.Core;

public interface IMetadataDetailsProvider
{
    Task<MetadataCandidate> GetDetailsAsync(MetadataCandidate candidate, CancellationToken token);
}
public static class MetadataText
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(200);
    public static string Plain(string text)
    {
        if (text.Length > 100000) throw new InvalidDataException("Provider text exceeds the field limit.");
        text = Regex.Replace(text, @"<(script|style)\b[^>]*>.*?</\1\s*>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline, Timeout);
        text = Regex.Replace(text, @"<br\s*/?>|</(?:p|div|li|h[1-6])\s*>", "\n", RegexOptions.IgnoreCase, Timeout);
        text = Regex.Replace(text, @"<[^>]*>", "", RegexOptions.Singleline, Timeout);
        text = WebUtility.HtmlDecode(text).Replace('\u00a0', ' ');
        text = Regex.Replace(text, @"\[/?(?:b|i|u|s|spoiler|quote|url)(?:=[^\]]*)?\]", "", RegexOptions.IgnoreCase, Timeout);
        text = Regex.Replace(text, @"[^\S\r\n]+", " ", RegexOptions.None, Timeout);
        text = Regex.Replace(text, @"[ \t]*\r?\n[ \t]*", "\n", RegexOptions.None, Timeout);
        return Regex.Replace(text, @"\n{3,}", "\n\n", RegexOptions.None, Timeout).Trim();
    }
    public static string Tags(IEnumerable<string> values) => string.Join(", ", values.Select(Plain).Select(t => Regex.Replace(t, @"\s+", " ", RegexOptions.None, Timeout).Trim()).Where(t => t.Length is > 0 and <= 200).Distinct(StringComparer.OrdinalIgnoreCase).Take(100));
    public static string SearchTitle(Game game)
    {
        if (!string.IsNullOrWhiteSpace(game.Naming.CanonicalTitle)) return game.Naming.CanonicalTitle;
        var title = Regex.Replace(game.Title, @"^(?:Ren'Py|Unity|RPGM|Godot|Unreal Engine)\s*[-–]\s*", "", RegexOptions.IgnoreCase, Timeout);
        if (Regex.IsMatch(title, @"\[v?\d", RegexOptions.IgnoreCase, Timeout)) title = Regex.Replace(title, @"\[[^\]]*\]", "", RegexOptions.None, Timeout);
        return FilenameParser.Parse(title).Title;
    }
    public static int PreferredProvider(ProviderCredentials credentials, bool associatedF95) => associatedF95 ? 6 : credentials.RawgKey.Length > 0 ? 5 : credentials.SteamKey.Length > 0 ? 1 : credentials.IgdbClientId.Length > 0 && credentials.IgdbClientSecret.Length > 0 ? 2 : 0;
    public static string UserAgent => "ArcHive/" + typeof(Game).Assembly.GetName().Version!.ToString(3);
    public static string Text(JsonElement row, string name) => row.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString()! : "";
    public static string[] Names(JsonElement row, string name) => row.TryGetProperty(name, out var list) && list.ValueKind == JsonValueKind.Array ? list.EnumerateArray().Select(x => Text(x, "name")).Where(x => x.Length > 0).ToArray() : [];
    public static void CheckIdentity(MetadataCandidate candidate, string provider)
    { if (candidate.Provider != provider || !long.TryParse(candidate.Id, out var id) || id <= 0) throw new ProviderException("Select a valid match from this provider first."); }
}

// Store details are fetched only for a selected catalog match. This public store endpoint
// is not the documented IStoreService contract, so failure leaves the catalog match usable.
public sealed class SteamStoreDetails(ProviderTransport transport) : IMetadataDetailsProvider
{
    public async Task<MetadataCandidate> GetDetailsAsync(MetadataCandidate candidate, CancellationToken token)
    {
        MetadataText.CheckIdentity(candidate, "steam");
        using var json = await transport.SendAsync(new(HttpMethod.Get, $"https://store.steampowered.com/api/appdetails?appids={candidate.Id}&l=english"), token);
        if (!json.RootElement.TryGetProperty(candidate.Id, out var entry) || !entry.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True || !entry.TryGetProperty("data", out var row)) throw new ProviderException("Steam store details are unavailable for this match. The catalog title can still be reviewed.");
        if (!row.TryGetProperty("steam_appid", out var appId) || appId.ToString() != candidate.Id) throw new ProviderException("Steam returned details for a different game.");
        var fields = new Dictionary<MetadataField,string>(candidate.Fields);
        var description = MetadataText.Text(row, "detailed_description"); if (description.Length == 0) description = MetadataText.Text(row, "short_description");
        if (description.Length > 0) fields[MetadataField.Description] = MetadataText.Plain(description);
        var tags = new List<string>();
        if (row.TryGetProperty("genres", out var genres) && genres.ValueKind == JsonValueKind.Array) tags.AddRange(genres.EnumerateArray().Select(x => MetadataText.Text(x, "description")));
        if (tags.Count > 0) fields[MetadataField.Tags] = MetadataText.Tags(tags);
        if (MetadataText.Text(row, "controller_support") == "full") fields[MetadataField.ControllerSupport] = "Full";
        if (row.TryGetProperty("release_date", out var release) && (!release.TryGetProperty("coming_soon", out var upcoming) || upcoming.ValueKind != JsonValueKind.True))
        {
            var year = Regex.Match(MetadataText.Text(release,"date"), @"\b(?:19|20)\d{2}\b"); if (year.Success) fields[MetadataField.ReleaseYear] = year.Value;
        }
        var title = MetadataText.Text(row,"name");
        var result = candidate with { Title = title.Length > 0 ? title : candidate.Title, Fields = fields, RetrievedAt = DateTimeOffset.UtcNow };
        MetadataValidation.Validate(result); return result;
    }
}
