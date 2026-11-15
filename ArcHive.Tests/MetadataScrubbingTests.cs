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
