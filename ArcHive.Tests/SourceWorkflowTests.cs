using System.Net;
using System.Text;
using System.Text.Json;
using ArcHive.Core;
internal static class SourceWorkflowTests
{
    private const string Thread = "https://f95zone.to/threads/moon-garden.12345/";
    private const string Page = """<html><head><meta property="og:title" content="[Ren'Py] Moon Garden [v1.2.3] [Studio] | F95zone"><meta content="https://f95zone.to/threads/moon-garden.12345/" property="og:url"><meta property='og:description' content='A &amp; B &lt;b&gt;story&lt;/b&gt;'></head></html>""";
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public int Count;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); Count++; return Task.FromResult(response(request)); }
    }
    private static HttpResponseMessage Html(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "text/html") };
    public static async Task Run(string root, Action<bool, string> check)
    {
        async Task Reject(Func<Task> action, string name)
        { try { await action(); } catch (Exception ex) when (ex is ProviderException or InvalidDataException or OperationCanceledException or JsonException or KeyNotFoundException) { check(true, name); return; } throw new Exception("Expected rejection: " + name); }
        check(F95ThreadSource.CanonicalUrl(Thread + "?token=not-stored#post-2") == "https://f95zone.to/threads/12345/", "F95 links discard slugs query strings and fragments before storage");
        check(F95ThreadSource.CanonicalUrl("https://f95zone.to/threads/12345") == "https://f95zone.to/threads/12345/", "F95 accepts numeric thread links without trailing slash");
        foreach (var bad in new[] { "http://f95zone.to/threads/12345/", "https://f95zone.to.evil.invalid/threads/12345/", "https://user:password@f95zone.to/threads/12345/", "https://f95zone.to:8443/threads/12345/", "https://f95zone.to/login/", "https://f95zone.to/threads/%31/", "file:///C:/test", "https://f95zone.to/threads/0/" })
            await Reject(() => Task.Run(() => F95ThreadSource.CanonicalUrl(bad)), "F95 rejects unsafe or unsupported link " + bad.Split('/')[0]);
        var parsed = F95ThreadSource.Parse(Thread, Page);
        check(parsed.Candidate.Title == "Moon Garden" && parsed.Candidate.Id == "12345" && parsed.Version == "v1.2.3", "F95 extracts clean title stable identity and conservative version evidence");
        check(parsed.Candidate.Fields[MetadataField.Engine] == "Ren'Py" && parsed.Candidate.Provider == "f95zone", "F95 engine observations retain provider provenance");
        check(parsed.Candidate.Artwork.Length == 0, "Thread lookup does not import unreviewed attachment URLs");
        await Reject(() => Task.Run(() => F95ThreadSource.Parse(Thread, Page.Replace("12345", "12346"))), "F95 mismatched canonical identity is rejected");
        await Reject(() => Task.Run(() => F95ThreadSource.Parse(Thread, "<title>Just a moment...</title>")), "F95 login and challenge pages are not treated as metadata");
        await Reject(() => Task.Run(() => F95ThreadSource.Parse(Thread, new string('x', 2_000_001))), "F95 parsing enforces page size limit");
        var manual = new Game { Title = "Manual title", Engine = "Manual engine" };
        var proposed = MetadataReconciliation.Preview(manual, parsed.Candidate);
        check(manual.Title == "Manual title" && manual.Engine == "Manual engine" && proposed.Any(p => p.Conflict), "F95 preview preserves manual fields and exposes conflicts");
        MetadataReconciliation.Apply(manual, parsed.Candidate, proposed.Where(p => p.Field == MetadataField.Engine).ToArray());
        check(manual.Title == "Manual title" && manual.Engine == "Ren'Py", "F95 applies only explicitly selected metadata fields");
        var handler = new Handler(r => { check(r.Headers.Authorization is null && !r.Headers.Contains("Cookie") && r.RequestUri!.Query.Length == 0, "Public F95 lookup sends no credentials or browser cookies"); return Html(Page); });
        using (var client = new HttpClient(handler)) check((await new F95ThreadSource(client).CheckAsync(Thread, default)).Version == "v1.2.3", "F95 public transport returns parsed evidence");
        var redirectHandler = new Handler(r => r.RequestUri!.AbsolutePath == "/threads/12345/" ? new(HttpStatusCode.MovedPermanently) { Headers = { Location = new(Thread) } } : Html(Page));
        using (var client = new HttpClient(redirectHandler)) { await new F95ThreadSource(client).CheckAsync(Thread, default); check(redirectHandler.Count == 2, "F95 follows only bounded same-thread canonical redirects"); }
        var offsite = new Handler(_ => new(HttpStatusCode.Redirect) { Headers = { Location = new("https://evil.invalid/") } });
        using (var client = new HttpClient(offsite)) { await Reject(() => new F95ThreadSource(client).CheckAsync(Thread, default), "F95 external redirect is rejected"); check(offsite.Count == 1, "No request reaches offsite redirect target"); }
        using (var client = new HttpClient(new Handler(_ => new(HttpStatusCode.Forbidden)))) await Reject(() => new F95ThreadSource(client).CheckAsync(Thread, default), "Login-required thread fails without an authentication bypass");
        using (var client = new HttpClient(new Handler(_ => Html(new string('x', 2_000_001))))) await Reject(() => new F95ThreadSource(client).CheckAsync(Thread, default), "F95 transport bounds response bytes");
        using (var cancelled = new CancellationTokenSource()) { cancelled.Cancel(); using var client = new HttpClient(handler); await Reject(() => new F95ThreadSource(client).CheckAsync(Thread, cancelled.Token), "F95 lookup supports cancellation"); }
        var steam = OwnedLibraryImport.Parse("""{"response":{"games":[{"appid":42,"name":"Moon Garden"},{"appid":42,"name":"Moon Garden"}]}}""", "steam");
        var gog = OwnedLibraryImport.Parse("""{"products":[{"id":99,"title":"Moon Garden","token":"secret-not-imported"}]}""", "gog");
        check(steam.Count == 1 && gog.Single().Provider == "gog", "Owned-library adapters normalize Steam and GOG identities");
        var original = new Library { Games = [new Game { Title = "Moon Garden", Favorite = true }] };
        var imported = OwnedLibraryImport.Apply(original, steam.Concat(gog));
        check(original.Games.Count == 1 && imported.Games.Count == 3 && imported.Games[0].Favorite, "Owned import preserves source library and never merges on title alone");
        check(imported.Games[1].State == "Not installed" && imported.Games[1].Installations.Count == 0 && imported.Games[1].Archives.Count == 0, "Owned import never invents local installations or launch profiles");
        check(OwnedLibraryImport.Apply(imported, steam.Concat(gog)).Games.Count == 3, "Owned-library repeated import is idempotent by provider ID");
        check(!JsonSerializer.Serialize(imported).Contains("secret-not-imported"), "Extra credential-like export fields never enter the library");
        await Reject(() => Task.Run(() => OwnedLibraryImport.Parse("""{"products":[{"id":"../bad","title":"Bad"}]}""", "gog")), "Owned import rejects malformed IDs");
        await Reject(() => Task.Run(() => OwnedLibraryImport.Parse(new string('x', 2_000_001), "steam")), "Owned import limits file size");
        await Reject(() => Task.Run(() => OwnedLibraryImport.Apply(original, [new("other", "1", "Bad")])), "Owned import revalidates identities at apply boundary");
        using (var client = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent("{\"response\":{}}") }))) await Reject(() => OwnedLibraryImport.FetchSteamAsync(new(client, ["api.steampowered.com"]), "fixture-key", "76561198000000000", default), "Steam private or unavailable library yields actionable failure");
        var steamHandler = new Handler(r => { check(r.RequestUri!.Host == "api.steampowered.com" && r.RequestUri.Query.Contains("include_appinfo=true"), "Steam owned lookup uses the documented endpoint with app details"); return new(HttpStatusCode.OK) { Content = new StringContent("{\"response\":{\"games\":[{\"appid\":42,\"name\":\"Moon Garden\"}]}}") }; });
        using (var client = new HttpClient(steamHandler)) { var owned = await OwnedLibraryImport.FetchSteamAsync(new(client, ["api.steampowered.com"]), "fixture-key", "76561198000000000", default); check(owned.Count == 1 && !JsonSerializer.Serialize(owned).Contains("fixture-key"), "Owned lookup returns game data without API credentials"); }
        check(GameWorkflow.Primary(null, null, null) == GamePrimaryAction.LinkFolder, "Uninstalled entries lead to linking a folder instead of a launch error");
        var installPath = Path.Combine(root, "workflow-install"); Directory.CreateDirectory(installPath);
        var installation = new GameInstallation { Path = installPath };
        check(GameWorkflow.Primary(installation, null, null) == GamePrimaryAction.ConfigureLaunch, "Folder-only games lead to launch setup");
        var archivePath = Path.Combine(root, "workflow.zip"); File.WriteAllText(archivePath, "fixture");
        check(GameWorkflow.Primary(null, null, new GameArchive { Path = archivePath }) == GamePrimaryAction.Extract, "Archive-only games lead to extraction");
        var executable = Path.Combine(installPath, "Game.exe"); File.WriteAllText(executable, "fixture"); var profile = new LaunchProfile { ExecutablePath = executable, WorkingDirectory = installPath }; installation.Profiles.Add(profile);
        check(GameWorkflow.Primary(installation, profile, null) == GamePrimaryAction.Play, "Validated launch profiles expose Play as the primary action");
        var prefs = new AppPreferences { EnableF95Zone = true, LibraryDensity = LibraryDensity.Compact, LibraryOrder = LibraryOrder.RecentlyPlayed, ControllerProfile = ControllerProfile.Xbox };
        var settings = new AppPreferencesStore(Path.Combine(root, "workflow-settings.json")); settings.Save(prefs); var reloaded = settings.Load();
        check(reloaded.EnableF95Zone && reloaded.LibraryDensity == LibraryDensity.Compact && reloaded.LibraryOrder == LibraryOrder.RecentlyPlayed, "Workflow settings round-trip without credentials");
        check(!new AppPreferences().EnableF95Zone, "F95 tools are opt-in for existing and new libraries");
        imported.Games[0].F95Thread = new() { Url = parsed.Url, ObservedTitle = parsed.Candidate.Title, ObservedVersion = parsed.Version, CheckedAt = parsed.Candidate.RetrievedAt };
        var store = new SqliteLibraryStore(Path.Combine(root, "workflow-library.db")); store.Load(); store.Save(imported); var loaded = store.Load();
        check(loaded.Games[0].F95Thread!.ObservedVersion == "v1.2.3" && loaded.Games[1].OwnedLibraryIds["steam"] == "42", "F95 evidence and owned-library associations persist in SQLite");
    }
}
