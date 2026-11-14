using System.Text.Json;
using System.Text.RegularExpressions;
namespace ArcHive.Core;
public sealed record OwnedGame(string Provider, string Id, string Title)
{
    public override string ToString() => Title;
}
public static class OwnedLibraryImport
{
    public static IReadOnlyList<OwnedGame> Parse(string json, string provider)
    {
        if (provider is not ("steam" or "gog")) throw new InvalidDataException("Unsupported library source.");
        if (json.Length > 2_000_000) throw new InvalidDataException("Library export exceeds 2 MB.");
        using var document = JsonDocument.Parse(json); var root = document.RootElement;
        var rows = provider == "steam" ? root.GetProperty("response").GetProperty("games") : root.GetProperty("products");
        if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() > 10000) throw new InvalidDataException("Invalid library export.");
        var items = new List<OwnedGame>();
        foreach (var row in rows.EnumerateArray())
        {
            var id = row.GetProperty(provider == "steam" ? "appid" : "id").ToString();
            var title = row.GetProperty(provider == "steam" ? "name" : "title").GetString()?.Trim() ?? "";
            if (!Regex.IsMatch(id, @"^[1-9][0-9]{0,19}$") || title.Length is 0 or > 500 || title.Any(char.IsControl)) throw new InvalidDataException("Invalid game identity in library export.");
            items.Add(new(provider, id, title));
        }
        return items.DistinctBy(g => (g.Provider, g.Id)).OrderBy(g => g.Title).ToArray();
    }
    public static async Task<IReadOnlyList<OwnedGame>> FetchSteamAsync(ProviderTransport transport, string key, string steamId, CancellationToken token)
    {
        if (key.Length == 0) throw new ProviderException("Configure a Steam Web API key in Sources first.");
        if (!Regex.IsMatch(steamId, @"^[0-9]{17}$")) throw new ProviderException("Enter your 17-digit SteamID64, not a username or password.");
        using var data = await transport.SendAsync(new(HttpMethod.Get, "https://api.steampowered.com/IPlayerService/GetOwnedGames/v0001/?key=" + Uri.EscapeDataString(key) + "&steamid=" + steamId + "&include_appinfo=true&format=json"), token);
        if (!data.RootElement.TryGetProperty("response", out var response) || !response.TryGetProperty("games", out _)) throw new ProviderException("Steam returned no visible games. Your game details may be private, or the account has no supported owned games.");
        return Parse(data.RootElement.GetRawText(), "steam");
    }
    public static Library Apply(Library library, IEnumerable<OwnedGame> chosen)
    {
        var items = chosen.ToArray(); if (items.Length > 10000) throw new InvalidDataException("Too many imported games.");
        var copy = JsonSerializer.Deserialize<Library>(JsonSerializer.Serialize(library))!;
        foreach (var item in items)
        {
            // Validate again at the mutation boundary, including callers other than the import dialog.
            if (item.Provider is not ("steam" or "gog") || !Regex.IsMatch(item.Id, @"^[1-9][0-9]{0,19}$") || string.IsNullOrWhiteSpace(item.Title) || item.Title.Length > 500 || item.Title.Any(char.IsControl)) throw new InvalidDataException("Invalid game identity.");
            if (copy.Games.Any(g => g.OwnedLibraryIds.GetValueOrDefault(item.Provider) == item.Id)) continue;
            copy.Games.Add(new Game { Title = item.Title, OwnedLibraryIds = new() { [item.Provider] = item.Id } });
        }
        return copy;
    }
}
