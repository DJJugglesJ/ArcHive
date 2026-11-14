using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ArcHive.Core;

internal static class ProviderTests
{
    private const string Secret = "fixture-secret-only";
    private const string Token = "fixture-access-token";
    private const string GameRows = """[{"id":42,"name":"Moon Garden","summary":"A moon adventure","first_release_date":1577836800,"alternative_names":[{"name":"Lunar Garden"}],"game_engines":[{"name":"RenPy"}],"genres":[{"name":"Adventure"}]}]""";
    private static readonly MetadataQuery Query = new("Moon Garden");
    public static async Task Run(Action<bool, string> check)
    {
        var rawgHandler = new Handler((_, _) => Ok("""{"results":[{"id":42,"name":"Moon Garden","released":"2020-01-01","genres":[{"name":"Adventure"}],"tags":[{"name":"Indie"}]}],"next":"https://evil.invalid/?key=secret"}"""));
        using var rawgClient = new HttpClient(rawgHandler);
        var rawg = new RawgMetadataProvider(Transport(rawgClient, "api.rawg.io"), Secret);
        var found = await rawg.SearchAsync(new("Moon & \"Garden\""), default);
        check(found[0].Provider == "rawg" && found[0].Id == "42" && found[0].Fields[MetadataField.ReleaseYear] == "2020" && found[0].Fields[MetadataField.Tags] == "Adventure, Indie", "RAWG maps identity year and tags");
        check(rawgHandler.Requests[0].Url.Contains("search=Moon%20%26%20%22Garden%22") && rawgHandler.Requests[0].Url.Contains("page_size=20") && rawgHandler.Requests[0].Authorization == "", "RAWG search safely encodes title and limits results");
        found[0].Fields.Clear(); var cached = await rawg.SearchAsync(new("Moon & \"Garden\""), default);
        check(rawgHandler.Requests.Count == 1 && cached[0].Fields.Count == 2, "RAWG cache isolates mutation and never follows next URL");
        check(!JsonSerializer.Serialize(cached).Contains(Secret), "RAWG candidate contains no API key");

        var igdbHandler = new Handler((r, _) => r.Uri.Host == "id.twitch.tv" ? Ok(TokenJson()) : Ok(GameRows));
        using var igdbClient = new HttpClient(igdbHandler);
        var igdb = new IgdbMetadataProvider(Transport(igdbClient, "api.igdb.com", "id.twitch.tv"), "client-id", Secret);
        var igdbResult = await igdb.SearchAsync(new("Moon \\\"; limit 500;"), default);
        check(igdbHandler.Requests[0].Method == "POST" && !igdbHandler.Requests[0].Url.Contains(Secret) && igdbHandler.Requests[0].Body.Contains("grant_type=client_credentials"), "IGDB exchanges client credentials in POST form");
        var gameRequest = igdbHandler.Requests[1];
        check(gameRequest.Authorization == "Bearer " + Token && gameRequest.ClientId == "client-id" && !gameRequest.Body.Contains(Secret), "IGDB sends token and client ID only to game endpoint");
        check(gameRequest.Body.StartsWith("search \"Moon \\\\\\\"; limit 500;\";") && gameRequest.Body.EndsWith("limit 20;"), "IGDB quotes and backslashes cannot escape search string");
        check(igdbResult[0].Aliases.Single() == "Lunar Garden" && igdbResult[0].Fields[MetadataField.Engine] == "RenPy" && igdbResult[0].Fields[MetadataField.ReleaseYear] == "2020" && igdbResult[0].Fields[MetadataField.Description] == "A moon adventure", "IGDB maps aliases engines descriptions and UTC release year");
        await igdb.SearchAsync(Query, default);
        check(igdbHandler.Requests.Count(r => r.Uri.Host == "id.twitch.tv") == 1, "IGDB reuses unexpired session token");
        var retryHandler = new Handler((r, i) => r.Uri.Host == "id.twitch.tv" ? Ok(TokenJson()) : i == 1 ? new(HttpStatusCode.Unauthorized) : Ok(GameRows));
        using var retryClient = new HttpClient(retryHandler);
        await new IgdbMetadataProvider(Transport(retryClient, "api.igdb.com", "id.twitch.tv"), "client-id", Secret).SearchAsync(Query, default);
        check(retryHandler.Requests.Count == 4, "IGDB renews token once after unauthorized response");
        var deniedHandler = new Handler((r, _) => r.Uri.Host == "id.twitch.tv" ? Ok(TokenJson()) : new(HttpStatusCode.Unauthorized));
        using var deniedClient = new HttpClient(deniedHandler);
        await Reject(() => new IgdbMetadataProvider(Transport(deniedClient, "api.igdb.com", "id.twitch.tv"), "client-id", Secret).SearchAsync(Query, default), "IGDB repeated unauthorized response fails without infinite retry", check);
        check(deniedHandler.Requests.Count == 4, "IGDB authentication retry is bounded");

        var steamHandler = new Handler((_, i) => Ok(i == 0
            ? """{"response":{"apps":[{"appid":1,"name":"Other Game"}],"have_more_results":true,"last_appid":1}}"""
            : """{"response":{"apps":[{"appid":42,"name":"Moon Garden"}],"have_more_results":false}}"""));
        using var steamClient = new HttpClient(steamHandler); var steam = new SteamMetadataProvider(Transport(steamClient, "api.steampowered.com"), Secret);
        var steamResult = await steam.SearchAsync(Query, default);
        check(steamHandler.Requests.Count == 2 && steamHandler.Requests[1].Url.Contains("last_appid=1"), "Steam walks catalog cursor");
        check(steamHandler.Requests[0].Url.Contains("include_dlc=false") && steamHandler.Requests[0].Url.Contains("include_games=true"), "Steam explicitly excludes DLC and non-game catalog entries");
        check(steamResult[0].Id == "42" && steamResult[0].Provider == "steam" && steamResult[0].Fields.Count == 0, "Steam maps catalog title without fabricating release date or description");
        await steam.SearchAsync(new("Other Game"), default);
        check(steamHandler.Requests.Count == 2, "Steam reuses complete catalog for another title");
        var malformedCatalogHandler = new Handler((_, i) => Ok(i == 0
            ? "{\"response\":{\"apps\":[{\"appid\":42,\"name\":\"" + new string('x', 501) + "\"}],\"have_more_results\":false}}"
            : "{\"response\":{\"apps\":[{\"appid\":42,\"name\":\"Moon Garden\"}],\"have_more_results\":false}}"));
        using var malformedCatalogClient = new HttpClient(malformedCatalogHandler);
        var malformedCatalog = new SteamMetadataProvider(Transport(malformedCatalogClient, "api.steampowered.com"), Secret);
        await Reject(() => malformedCatalog.SearchAsync(Query, default), "Malformed catalog rejected before cache publication", check);
        check((await malformedCatalog.SearchAsync(Query, default)).Single().Id == "42" && malformedCatalogHandler.Requests.Count == 2, "Catalog validation failure does not poison subsequent retry");
        using var loopClient = new HttpClient(new Handler((_, _) => Ok("""{"response":{"apps":[{"appid":1,"name":"Moon Garden"}],"have_more_results":true}}""")));
        await Reject(() => new SteamMetadataProvider(Transport(loopClient, "api.steampowered.com"), Secret).SearchAsync(Query, default), "Steam rejects nonadvancing pagination", check);

        var itchHandler = new Handler((_, i) => Ok(i switch
        {
            0 => Owned(1, 42), 1 => Owned(2, 42), _ => "{\"owned_keys\":[]}"
        }));
        using var itchClient = new HttpClient(itchHandler); var itch = new ItchMetadataProvider(Transport(itchClient, "api.itch.io"), Secret);
        var itchResult = await itch.SearchAsync(Query, default);
        check(itchHandler.Requests.Count == 3 && itchHandler.Requests[2].Url.Contains("page=3"), "itch.io continues short owned-key pages until empty");
        check(itchResult.Count == 1 && itchResult[0].Id == "42" && itchResult[0].Provider == "itch", "itch.io deduplicates multiple keys by game identity");
        check(itchHandler.Requests.All(r => r.Authorization == "Bearer " + Secret && !r.Url.Contains(Secret)), "itch.io credentials stay in Authorization header");
        check(!itchResult[0].Fields.ContainsKey(MetadataField.ReleaseYear) && !JsonSerializer.Serialize(itchResult).Contains("purchase_id"), "itch.io discards account details and does not mislabel publication date");
        await itch.SearchAsync(new("Lunar Garden"), default);
        check(itchHandler.Requests.Count == 3, "itch.io catalog cache avoids repeated account reads");
        var projectsHandler = new Handler((_, _) => Ok("""{"games":[{"id":7,"title":"Moon Garden","short_text":"Project summary","classification":"game"},{"id":8,"title":"Asset Pack","classification":"asset"}]}"""));
        using var projectsClient = new HttpClient(projectsHandler);
        var projects = await new ItchMetadataProvider(Transport(projectsClient, "api.itch.io"), Secret, true).SearchAsync(Query, default);
        check(projectsHandler.Requests.Single().Uri.AbsolutePath == "/profile/games" && projects.Single().Fields[MetadataField.Description] == "Project summary", "itch.io project scope maps short text and excludes assets");
        using var itchLoopClient = new HttpClient(new Handler((_, _) => Ok(Owned(1, 42))));
        await Reject(() => new ItchMetadataProvider(Transport(itchLoopClient, "api.itch.io"), Secret).SearchAsync(Query, default), "itch.io rejects repeated owned keys across pages", check);
        using var itchErrorClient = new HttpClient(new Handler((_, _) => Ok("{\"errors\":[\"" + Secret + "\"]}")));
        await Reject(() => new ItchMetadataProvider(Transport(itchErrorClient, "api.itch.io"), Secret).SearchAsync(Query, default), "itch.io sanitizes body-level API errors", check);

        // The same transport protections apply to all four adapters.
        foreach (var kind in new[] { "steam", "igdb", "itch", "rawg" })
        {
            var h = new Handler((_, _) => throw new Exception("Unexpected request " + Secret)); using var client = new HttpClient(h);
            IMetadataProvider Missing() => kind switch
            {
                "steam" => new SteamMetadataProvider(Transport(client, "api.steampowered.com"), ""),
                "igdb" => new IgdbMetadataProvider(Transport(client, "id.twitch.tv", "api.igdb.com"), "id", ""),
                "itch" => new ItchMetadataProvider(Transport(client, "api.itch.io"), ""),
                _ => new RawgMetadataProvider(Transport(client, "api.rawg.io"), "")
            };
            await Reject(() => Missing().SearchAsync(Query, default), kind + " missing credentials rejected before network", check);
            check(h.Requests.Count == 0, kind + " missing credentials send no request");
        }
        foreach (var status in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.ServiceUnavailable, HttpStatusCode.Redirect })
        {
            using var client = new HttpClient(new Handler((_, _) => new(status) { Content = new StringContent(Secret) }));
            await Reject(() => new RawgMetadataProvider(Transport(client, "api.rawg.io"), Secret).SearchAsync(Query, default), "Sanitized provider HTTP " + (int)status, check);
        }
        var limitHandler = new Handler((_, _) => { var r = new HttpResponseMessage(HttpStatusCode.TooManyRequests); r.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(60)); return r; });
        using var limitClient = new HttpClient(limitHandler); var limited = new RawgMetadataProvider(Transport(limitClient, "api.rawg.io"), Secret);
        await Reject(() => limited.SearchAsync(Query, default), "Provider rate limit reported", check);
        await Reject(() => limited.SearchAsync(Query, default), "Provider backoff blocks retry", check);
        check(limitHandler.Requests.Count == 1, "Provider backoff prevents another HTTP call");
        foreach (var payload in new[] { "{invalid", new string('x', 2_000_001), "{\"results\":[{\"id\":-1,\"name\":\"Bad\"}]}" })
        {
            using var client = new HttpClient(new Handler((_, _) => Ok(payload)));
            await Reject(() => new RawgMetadataProvider(Transport(client, "api.rawg.io"), Secret).SearchAsync(Query, default), "Invalid or oversized provider response rejected", check);
        }
        var networkHandler = new Handler((_, _) => throw new HttpRequestException("Request failed https://example/?key=" + Secret));
        using var networkClient = new HttpClient(networkHandler);
        await Reject(() => new RawgMetadataProvider(Transport(networkClient, "api.rawg.io"), Secret).SearchAsync(Query, default), "Network exception never exposes credential URL", check);
        var hostHandler = new Handler((_, _) => Ok("{}")); using var hostClient = new HttpClient(hostHandler);
        await Reject(async () => { using var _ = await Transport(hostClient, "api.rawg.io").SendAsync(new(HttpMethod.Get, "https://evil.invalid/"), default); }, "Provider transport rejects unapproved hosts", check);
        check(hostHandler.Requests.Count == 0, "Unapproved host receives no request");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { await rawg.SearchAsync(Query, cancelled.Token); throw new Exception("Cancellation ignored"); }
        catch (OperationCanceledException) { check(true, "Provider canceled before search"); }
        using var slowClient = new HttpClient(new Handler((_, _) => Ok("{}")) { Slow = true }); using var during = new CancellationTokenSource(40);
        try { await new RawgMetadataProvider(Transport(slowClient, "api.rawg.io"), Secret).SearchAsync(Query, during.Token); throw new Exception("Cancellation ignored"); }
        catch (OperationCanceledException) { check(true, "Provider in-flight cancellation"); }
        var isolated = new RawgMetadataProvider(Transport(rawgClient, "api.rawg.io"), "another-fixture-key"); await isolated.SearchAsync(Query, default);
        check(rawgHandler.Requests.Count == 2, "Different credential instances do not share search cache");
        check(!new ProviderCredentials { RawgKey = Secret }.ToString()!.Contains(Secret), "Credential object string never exposes secrets");
        var vault = new WindowsProviderCredentialStore("ArcHive.Tests/Providers/" + Guid.NewGuid());
        var credentialCreated = false;
        try
        {
            check(vault.Load().RawgKey == "", "Missing Windows credential returns empty configuration");
            vault.Save(new() { SteamKey = Secret, IgdbClientId = "client", IgdbClientSecret = "secret2", ItchKey = "itch", RawgKey = "rawg" }); credentialCreated = true;
            var saved = vault.Load();
            check(saved.SteamKey == Secret && saved.IgdbClientId == "client" && saved.IgdbClientSecret == "secret2" && saved.ItchKey == "itch" && saved.RawgKey == "rawg", "Windows Credential Manager round trip for all providers");
            vault.Save(new() { RawgKey = "replacement" });
            check(vault.Load().SteamKey == "" && vault.Load().RawgKey == "replacement", "Windows credential replacement removes previous keys");
            await Reject(() => { vault.Save(new() { RawgKey = new string('x', 3000) }); return Task.CompletedTask; }, "Oversized credentials rejected before replacement", check);
            check(vault.Load().RawgKey == "replacement", "Rejected credential save preserves prior settings");
            vault.Delete(); check(vault.Load().RawgKey == "", "Windows credential deletion");
        }
        finally { if (credentialCreated) vault.Delete(); }
    }
    private static ProviderTransport Transport(HttpClient client, params string[] hosts) => new(client, hosts, TimeSpan.Zero);
    private static string TokenJson() => "{\"access_token\":\"" + Token + "\",\"expires_in\":3600,\"token_type\":\"bearer\"}";
    private static string Owned(int key, int game) => "{\"owned_keys\":[{\"id\":" + key + ",\"purchase_id\":999,\"game\":{\"id\":" + game + ",\"title\":\"Moon Garden\",\"classification\":\"game\",\"published_at\":\"2020-01-01\"}}]}";
    private static HttpResponseMessage Ok(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json) };
    private static async Task Reject(Func<Task> action, string name, Action<bool, string> check)
    {
        try { await action(); throw new Exception("Expected rejection: " + name); }
        catch (ProviderException ex) { check(!ex.ToString().Contains(Secret) && !ex.ToString().Contains(Token), name); }
    }
    private sealed record Request(Uri Uri, string Method, string Body, string Authorization, string ClientId)
    { public string Url => Uri.AbsoluteUri; }
    private sealed class Handler(Func<Request, int, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Request> Requests { get; } = [];
        public bool Slow { get; init; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var entry = new Request(request.RequestUri!, request.Method.Method, request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken), request.Headers.Authorization?.ToString() ?? "", request.Headers.TryGetValues("Client-ID", out var ids) ? ids.Single() : "");
            Requests.Add(entry);
            if (Slow) await Task.Delay(Timeout.Infinite, cancellationToken);
            return respond(entry, Requests.Count - 1);
        }
    }
}
