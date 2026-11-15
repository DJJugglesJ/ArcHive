using System.Net;
using System.Text.Json;
using ArcHive.Core;
internal static class MetadataScrubbingTests
{
    private const string Url = "https://f95zone.to/threads/65211/";
    // Synthetic XenForo structure modeled on the user-supplied public thread; no copied post text.
    private const string Html = """
        <html><head><meta property="og:title" content="Unreal Engine - Carnal Instinct [v0.7.9 Halloween] [Team Carnal Instinct]">
        <meta property="og:url" content="https://f95zone.to/threads/example.65211/">
        <meta property="og:description" content="Short social preview"></head>
        <body><a href="/tags/adventure/" class="tagItem tagItem--tag_adventure">Adventure</a>
        <a class='tagItem' href='/tags/3d/'>3D game</a><a class='tagItem'>adventure</a>
        <article class="message-threadStarterPost"><b>Overview</b>:<br />A synthetic exploration game.<br />Two paragraphs of useful information.<br />
        <b>Thread Updated</b>: 2026-10-31<br /><b>Version</b>: v0.7.9 Halloween<br />Download links are not metadata.</article>
        <article><a class="tagItem">Unrelated Reply Tag</a>Reply text</article></body></html>
        """;
    public static async Task Run(string root, Action<bool,string> check)
    {
        async Task Reject(Func<Task> action, string name)
        { try { await action(); } catch(Exception ex) when(ex is ProviderException or InvalidDataException or OperationCanceledException) { check(true,name); return; } throw new Exception("Expected rejection: "+name); }
        var named = new Game { Title = "Unreal Engine - Carnal Instinct [v0.7.9 Halloween] [Team Carnal Instinct]" };
        check(MetadataText.SearchTitle(named) == "Carnal Instinct" && named.Title.StartsWith("Unreal Engine"), "Metadata search strips source release labels without changing the saved title");
        var parsed = F95ThreadSource.Parse(Url,Html);
        check(parsed.Candidate.Title == "Carnal Instinct" && parsed.Candidate.Fields[MetadataField.Engine] == "Unreal Engine", "F95 engine prefix is separate from the search title");
        check(parsed.Version == "v0.7.9 Halloween" && parsed.Candidate.ObservedRelease?.Version == "v0.7.9" && parsed.Candidate.ObservedRelease.Channel == "Halloween", "F95 labeled releases retain numeric version and channel separately");
        check(parsed.Candidate.Fields[MetadataField.Tags] == "Adventure, 3D game", "F95 imports distinct thread tags without reply tags");
        check(parsed.Candidate.Fields[MetadataField.Description].Contains("Two paragraphs") && !parsed.Candidate.Fields[MetadataField.Description].Contains("Download links"), "F95 full Overview replaces truncated social description and stops before release fields");
        check(F95MetadataProvider.Locator("65211") == Url && F95MetadataProvider.Locator("https://f95zone.to/threads/example.65211/") == Url, "F95 metadata provider accepts thread URL or numeric ID");
        await Reject(() => Task.Run(() => F95MetadataProvider.Locator("Carnal Instinct")), "F95 asks for a thread rather than pretending to provide title search");
        var blocked = Html.Replace("Unreal Engine - Carnal Instinct [v0.7.9 Halloween] [Team Carnal Instinct]", "Just a moment...");
        await Reject(() => Task.Run(() => F95ThreadSource.Parse(Url,blocked)), "F95 challenge pages cannot become game information");
        using var sourceClient = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent(Html,System.Text.Encoding.UTF8,"text/html") }));
        var f95 = new F95MetadataProvider(new(sourceClient));
        var candidates = await f95.SearchAsync(new("65211"),default); check(candidates.Single().Provider == "f95zone", "F95 participates in the normal metadata provider contract");
        var game = new Game { Title = "Manual title", Tags = "Personal", LatestRelease = new() { Version="0.6" } };
        var proposals = MetadataReconciliation.Preview(game,parsed.Candidate);
        MetadataReconciliation.Apply(game,parsed.Candidate,proposals.Where(p=>p.Field is MetadataField.Description or MetadataField.Tags).ToArray());
        check(game.Title == "Manual title" && game.Tags == "Adventure, 3D game" && game.LatestRelease.Version == "0.6", "F95 selected fields apply while title and unselected release stay unchanged");
        F95ThreadSource.ApplyEvidence(game,parsed.Candidate,false);
        check(game.F95Thread?.Url == Url && game.LatestRelease.Version == "0.6", "F95 metadata application links the thread without overwriting an unselected release");
        F95ThreadSource.ApplyEvidence(game,parsed.Candidate,true);
        check(game.LatestRelease.Version == "v0.7.9" && game.LatestRelease.Channel == "Halloween" && game.LatestReleaseSource == Url && game.ReleaseHistory.Count == 1, "Explicit F95 release choice persists version channel source and history");
        F95ThreadSource.ApplyEvidence(game,parsed.Candidate,true);
        check(game.ReleaseHistory.Count == 1, "Repeated F95 release application does not duplicate observations");
        var store = new SqliteLibraryStore(Path.Combine(root,"metadata-scrub.db"));store.Load();store.Save(new(){Games=[game]});
        var reloaded = store.Load().Games.Single();check(reloaded.Tags == game.Tags && reloaded.Description == game.Description && reloaded.Metadata.Fields[MetadataField.Tags].Provider == "f95zone", "Scrubbed information and tag provenance survive SQLite reload");
        MetadataReconciliation.Undo(game);check(game.Tags == "Personal" && game.Description == "", "Scrubbed fields support existing protected undo");
        check(VersionTracking.Compare(new(){Version="0.7.9",Channel="Halloween"},new(){Version="0.7.8"}) is null, "Different F95 release channels cannot imply an update automatically");
        check(MetadataText.Plain("<p>First &amp; second</p><script>secret()</script><p>[b]Third[/b]</p>") == "First & second\nThird", "Metadata descriptions scrub HTML, scripts and formatting while preserving paragraphs");
        using var spellingClient = new HttpClient(new Handler(_ => Json("""{"response":{"apps":[{"appid":3684710,"name":"My sexy Neighbour 2"}],"have_more_results":false}}""")));
        var spellingProvider = new SteamMetadataProvider(new(spellingClient,["api.steampowered.com"],TimeSpan.Zero),"fixture-key");
        var americanMatches = await spellingProvider.SearchAsync(new("My Sexy Neighbor 2"),default);
        var britishMatches = await spellingProvider.SearchAsync(new("My Sexy Neighbour 2"),default);
        check(americanMatches.Single().Id == "3684710" && britishMatches.Single().Id == "3684710", "Steam catalog returns the sequel with either Neighbor or Neighbour spelling");
        check(MetadataMatching.Rank(new("My Sexy Neighbor 2"),americanMatches).Single().Score == .8, "Neighbor and Neighbour are equivalent for title matching");
        var originalGame = americanMatches.Single() with { Id="2657120", Title="My Sexy Neighbor" };
        check(MetadataMatching.Rank(new("My Sexy Neighbor 2"),americanMatches.Append(originalGame)).First().Candidate.Id == "3684710", "Exact sequel outranks original despite regional spelling");
        var dated = F95ThreadSource.Parse(Url,Html.Replace("Unreal Engine - Carnal Instinct [v0.7.9 Halloween] [Team Carnal Instinct]", "Unity - Completed - My sexy Neighbour 2 [2025-12-11] [Example Studio]"));
        check(dated.Candidate.Title == "My sexy Neighbour 2" && dated.Candidate.Fields[MetadataField.Engine] == "Unity", "F95 status labels are removed without losing engine evidence");
        check(dated.Version == "2025-12-11" && dated.Candidate.ObservedRelease?.Version == "2025-12-11", "F95 date-based release is retained as version evidence");
        check(VersionTracking.Compare(dated.Candidate.ObservedRelease!,new(){Version="2025-12-10"}) is null, "Date-based releases remain conservatively incomparable");
        using var emptyItch = new HttpClient(new Handler(_ => Json("""{"owned_keys":{},"page":1,"per_page":500}""")));
        check((await new ItchMetadataProvider(new(emptyItch,["api.itch.io"],TimeSpan.Zero),"fixture").SearchAsync(new("Test"),default)).Count == 0, "itch.io empty-object terminal page is an empty library");
        var itchPage = 0;
        using var pagedItch = new HttpClient(new Handler(_ => ++itchPage == 1 ? Json("""{"owned_keys":[{"id":1,"game":{"id":42,"title":"Test"}}]}""") : Json("""{"owned_keys":{}}""")));
        check((await new ItchMetadataProvider(new(pagedItch,["api.itch.io"],TimeSpan.Zero),"fixture").SearchAsync(new("Test"),default)).Single().Id == "42" && itchPage == 2, "itch.io retains earlier matches when final page is an empty object");
        using var malformedItch = new HttpClient(new Handler(_ => Json("""{"owned_keys":{"unexpected":true}}""")));
        await Reject(()=>new ItchMetadataProvider(new(malformedItch,["api.itch.io"],TimeSpan.Zero),"fixture").SearchAsync(new("Test"),default),"itch.io nonempty object cannot silently terminate pagination");
        var requested = "";
        using var vndbClient = new HttpClient(new Handler(r => { requested=r.Content!.ReadAsStringAsync().GetAwaiter().GetResult(); return Json("""{"results":[{"id":"v42","title":"Test","description":"[b]Clean[/b] text","tags":[{"name":"Mystery","spoiler":0,"rating":2,"lie":false},{"name":"Plot Twist","spoiler":2,"rating":2},{"name":"False Tag","spoiler":0,"lie":true}]}]}"""); }));
        var vndb = (await new VndbMetadataProvider(vndbClient).SearchAsync(new("Test"),default)).Single();
        check(requested.Contains("tags.name") && requested.Contains("tags.spoiler"), "VNDB explicitly requests tag names and spoiler evidence");
        check(vndb.Fields[MetadataField.Tags] == "Mystery" && vndb.Fields[MetadataField.Description] == "Clean text", "VNDB maps only non-spoiler non-lie tags and cleans description markup");
        var rawgMatch = new MetadataCandidate("rawg","42","Test",[],new(){[MetadataField.Tags]="Adventure"},DateTimeOffset.UtcNow);
        using var rawgClient = new HttpClient(new Handler(r => { check(r.RequestUri!.AbsolutePath=="/api/games/42", "RAWG detail lookup targets only the selected identity"); return Json("""{"id":42,"name":"Test","description_raw":"Full game description","genres":[{"name":"Adventure"}],"tags":[{"name":"Exploration"}]}"""); }));
        var rawg = new RawgMetadataProvider(new(rawgClient,["api.rawg.io"],TimeSpan.Zero),"fixture-key");
        var detailed = await rawg.GetDetailsAsync(rawgMatch,default);
        check(detailed.Fields[MetadataField.Description] == "Full game description" && detailed.Fields[MetadataField.Tags] == "Adventure, Exploration", "RAWG details fill descriptions and tags missing from search");
        check(!rawgMatch.Fields.ContainsKey(MetadataField.Description), "Detail retrieval does not mutate cached search candidates");
        using var badClient = new HttpClient(new Handler(_=>Json("""{"id":99,"name":"Wrong"}""")));
        await Reject(()=>new RawgMetadataProvider(new(badClient,["api.rawg.io"]),"fixture").GetDetailsAsync(rawgMatch,default),"RAWG mismatched detail identity rejected");
        var steamMatch = new MetadataCandidate("steam","400","Portal",[],[],DateTimeOffset.UtcNow);
        using var steamClient = new HttpClient(new Handler(r => {check(r.RequestUri!.Host=="store.steampowered.com" && !r.RequestUri.Query.Contains("key="),"Steam store detail request never sends catalog credentials");return Json("""{"400":{"success":true,"data":{"steam_appid":400,"name":"Portal","detailed_description":"<p>Full description</p>","genres":[{"description":"Action"}],"release_date":{"coming_soon":false,"date":"10 Oct, 2007"},"controller_support":"full"}}}""");}));
        var steam = await new SteamStoreDetails(new(steamClient,["store.steampowered.com"],TimeSpan.Zero)).GetDetailsAsync(steamMatch,default);
        check(steam.Fields[MetadataField.Description] == "Full description" && steam.Fields[MetadataField.Tags] == "Action" && steam.Fields[MetadataField.ReleaseYear] == "2007", "Steam selected-match enrichment supplies real description genres and release year");
        using var unavailable = new HttpClient(new Handler(_=>Json("""{"400":{"success":false}}""")));
        await Reject(()=>new SteamStoreDetails(new(unavailable,["store.steampowered.com"])).GetDetailsAsync(steamMatch,default),"Unavailable Steam details report failure without fabricating fields");
        using var cancel = new CancellationTokenSource();cancel.Cancel();await Reject(()=>rawg.GetDetailsAsync(rawgMatch,cancel.Token),"Metadata details honor cancellation");
        var invalid = parsed.Candidate with { ObservedRelease = new(){Version=new string('x',129)} };
        await Reject(()=>Task.Run(()=>MetadataValidation.Validate(invalid)),"Oversized provider version evidence is rejected");
    }
    private static HttpResponseMessage Json(string text) => new(HttpStatusCode.OK){Content=new StringContent(text,System.Text.Encoding.UTF8,"application/json")};
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> respond) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) { token.ThrowIfCancellationRequested();return Task.FromResult(respond(request)); } }
}
