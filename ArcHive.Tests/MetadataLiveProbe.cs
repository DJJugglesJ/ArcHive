using ArcHive.Core;
using System.Text.Json;

// Explicit diagnostic only: never runs in the automated suite, prints no credentials or descriptions.
internal static class MetadataLiveProbe
{
    public static async Task Run(string title, string? steamAppId, string? providerFilter = null)
    {
        var credentials = new WindowsProviderCredentialStore().Load();
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        var providers = new (string Name, bool Ready, IMetadataProvider Provider)[]
        {
            ("Steam", credentials.SteamKey.Length > 0, new SteamMetadataProvider(new(client, ["api.steampowered.com"], TimeSpan.FromSeconds(1)), credentials.SteamKey)),
            ("IGDB", credentials.IgdbClientId.Length > 0 && credentials.IgdbClientSecret.Length > 0, new IgdbMetadataProvider(new(client, ["api.igdb.com", "id.twitch.tv"]), credentials.IgdbClientId, credentials.IgdbClientSecret)),
            ("RAWG", credentials.RawgKey.Length > 0, new RawgMetadataProvider(new(client, ["api.rawg.io"], TimeSpan.FromSeconds(1)), credentials.RawgKey)),
            ("itch.io owned", credentials.ItchKey.Length > 0, new ItchMetadataProvider(new(client, ["api.itch.io"], TimeSpan.FromSeconds(1)), credentials.ItchKey)),
            ("VNDB", true, new VndbMetadataProvider(client))
        };
        foreach (var item in providers.Where(p => providerFilter is null || p.Provider.Id == providerFilter))
        {
            if (!item.Ready) { Console.WriteLine(item.Name + ": not configured in Windows Credential Manager"); continue; }
            try
            {
                var results = await item.Provider.SearchAsync(new(title), default);
                var ranked = MetadataMatching.Rank(new(title),results);
                Console.WriteLine(JsonSerializer.Serialize(new { Provider = item.Name, Count = results.Count, Matches = ranked.Take(5).Select(m => new { m.Candidate.Id, m.Candidate.Title, m.Score }) }));
                if (item.Provider is IMetadataDetailsProvider detailsProvider && ranked.FirstOrDefault() is { } top)
                {
                    var detail = await detailsProvider.GetDetailsAsync(top.Candidate,default);
                    Console.WriteLine(JsonSerializer.Serialize(new { Provider=item.Name + " details", detail.Id, detail.Title, DescriptionLength=detail.Fields.GetValueOrDefault(MetadataField.Description)?.Length, TagCount=detail.Fields.GetValueOrDefault(MetadataField.Tags)?.Split(',').Length ?? 0 }));
                }
            }
            catch (ProviderException ex) { Console.WriteLine(item.Name + ": " + ex.Message); }
            catch (Exception ex) { Console.WriteLine(item.Name + ": " + ex.GetType().Name); }
        }
        if (steamAppId is null) return;
        try
        {
            var details = await new SteamStoreDetails(new(client, ["store.steampowered.com"])).GetDetailsAsync(new("steam", steamAppId, title, [], [], DateTimeOffset.UtcNow),default);
            Console.WriteLine(JsonSerializer.Serialize(new { Provider="Steam public details", details.Id, details.Title, DescriptionLength=details.Fields.GetValueOrDefault(MetadataField.Description)?.Length, Tags=details.Fields.GetValueOrDefault(MetadataField.Tags) }));
        }
        catch (ProviderException ex) { Console.WriteLine("Steam public details: " + ex.Message); }
    }
}
