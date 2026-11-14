using System.Net;
using System.Text.Json;
using ArcHive.Core;

internal static class MetadataTests
{
    public static async Task Run(string root, Action<bool, string> check)
    {
        var now = DateTimeOffset.UtcNow;
        MetadataCandidate Candidate(string id = "1", string title = "Moon Garden") => new("fixture", id, title, ["Lunar Garden"],
            new() { [MetadataField.Description] = "Provider description", [MetadataField.ReleaseYear] = "2020", [MetadataField.Engine] = "RenPy" }, now);
        var candidate = Candidate(); var query = new MetadataQuery("Moon Garden", "2020", "RenPy");
        var match = MetadataMatching.Rank(query, [candidate]).Single();
        check(Math.Abs(match.Score - 1) < .001 && match.Evidence.Length == 3, "Metadata title/year/engine evidence");
        check(MetadataMatching.Rank(new("Lunar Garden"), [candidate])[0].Evidence[0].StartsWith("Alias"), "Metadata alias matching");
        check(MetadataMatching.Rank(new("ＭＯＯＮ garden!"), [candidate])[0].Score == .8, "Metadata Unicode and punctuation normalization");
        check(MetadataMatching.Rank(new("Moon Garden 2"), [candidate])[0].Score < .8, "Metadata sequel numbers are retained");
        check(MetadataMatching.Rank(new(""), [candidate])[0].Score == 0, "Empty metadata query has zero similarity");
        check(MetadataMatching.Rank(new("Moon Garden", "2021", "Unity"), [candidate])[0].Score < .4, "Conflicting year and engine lower confidence");
        check(MetadataMatching.Rank(query, [candidate, Candidate("2")]).All(m => m.Ambiguous), "Equal candidates require ambiguity review");
        check(MetadataMatching.Rank(query, [candidate, candidate]).Count == 1, "Provider identity deduplicates candidates");
        check(MetadataMatching.Rank(query, [candidate, candidate with { Provider = "other" }]).Count == 2, "Provider IDs are namespaced");
        var game = new Game { Title = "My title", Description = "My notes" };
        var preview = MetadataReconciliation.Preview(game, candidate);
        check(game.Title == "My title" && preview.Single(p => p.Field == MetadataField.Description).Conflict, "Metadata preview is read-only and marks conflicts");
        MetadataReconciliation.Apply(game, candidate, preview.Where(p => p.Field == MetadataField.ReleaseYear).ToArray());
        check(game.Title == "My title" && game.Description == "My notes" && game.ReleaseYear == "2020", "Only selected fields are reconciled");
        check(game.Metadata.Fields[MetadataField.ReleaseYear].RetrievedAt == now && game.Metadata.Fields[MetadataField.ReleaseYear].ProviderId == "1", "Metadata stores source ID and retrieval time");
        MetadataReconciliation.SetManual(game, MetadataField.Description, "Edited notes");
        var manual = game.Metadata.Fields[MetadataField.Description];
        check(manual.Manual && manual.Provider == "Manual", "Manual edits record provenance");
        var beforeApply = MetadataReconciliation.Preview(game, candidate);
        MetadataReconciliation.Apply(game, candidate, beforeApply.Where(p => p.Field == MetadataField.Description).ToArray());
        MetadataReconciliation.Undo(game);
        check(game.Description == "Edited notes" && game.Metadata.Fields[MetadataField.Description] == manual, "Undo restores manual value and provenance");
        var stale = MetadataReconciliation.Preview(game, candidate);
        MetadataReconciliation.SetManual(game, MetadataField.Description, "Newer edit");
        Reject(() => MetadataReconciliation.Apply(game, candidate, stale), "Stale preview rejected atomically", check);
        check(game.Title == "My title", "Failed reconciliation leaves other fields unchanged");
        var fresh = MetadataReconciliation.Preview(game, candidate);
        Reject(() => MetadataReconciliation.Apply(game, candidate, [fresh[0], fresh[0]]), "Duplicate field selections rejected", check);
        MetadataReconciliation.SetManual(game, MetadataField.ReleaseYear, "2022");
        Reject(() => MetadataReconciliation.Undo(game), "Undo protects subsequent manual edits", check);
        var db = new SqliteLibraryStore(Path.Combine(root, "metadata.db")); db.Load();
        db.Save(new Library { Games = [game] });
        var loaded = db.Load().Games.Single();
        check(loaded.Metadata.Fields[MetadataField.Description].Manual && loaded.ReleaseYear == "2022" && loaded.Metadata.Undo.Count == 1, "SQLite round trip retains metadata provenance and undo");
        check(new Game().Metadata.Fields.Count == 0, "Legacy games default to empty metadata state");
        var undoGame = new Game { Title = "Original" };
        MetadataReconciliation.Apply(undoGame, candidate, MetadataReconciliation.Preview(undoGame, candidate));
        db.Save(new Library { Games = [undoGame] });
        var undoLoaded = db.Load().Games.Single(); MetadataReconciliation.Undo(undoLoaded);
        check(undoLoaded.Title == "Original" && undoLoaded.Metadata.Fields.Count == 0 && undoLoaded.ReleaseYear == "", "Persisted undo restores all selected fields and absent provenance");
        var legacy = JsonSerializer.Deserialize<Game>("{\"Title\":\"Legacy\"}")!;
        check(legacy.Metadata.Fields.Count == 0 && legacy.Engine == "" && legacy.Title == "Legacy", "Old metadata documents deserialize without migration");
        var json = JsonSerializer.Serialize(new[] { candidate });
        check((await OfflineMetadataProvider.Parse(json).SearchAsync(query, default)).Single().Id == "1", "Offline provider imports candidates");
        Reject(() => OfflineMetadataProvider.Parse("null"), "Offline null payload rejected", check);
        Reject(() => OfflineMetadataProvider.Parse(JsonSerializer.Serialize(new[] { candidate with { Id = "" } })), "Missing stable identity rejected", check);
        Reject(() => OfflineMetadataProvider.Parse(new string(' ', 2_000_001)), "Oversized offline input rejected", check);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        try { await OfflineMetadataProvider.Parse(json).SearchAsync(query, canceled.Token); throw new Exception("Cancellation ignored"); }
        catch (OperationCanceledException) { check(true, "Offline search cancellation"); }
        var handler = new Handler(); using var client = new HttpClient(handler); var provider = new VndbMetadataProvider(client);
        var found = await provider.SearchAsync(query, default);
        check(found.Single().Provider == "vndb" && found[0].Id == "v42" && found[0].Fields[MetadataField.ReleaseYear] == "2020", "VNDB response maps stable ID and partial release date");
        check(handler.Url == "https://api.vndb.org/kana/vn" && handler.Body.Contains("Moon Garden") && !handler.Body.Contains("RenPy"), "VNDB request sends only search title and fixed fields");
        found[0].Fields[MetadataField.Description] = "tampered";
        var cached = await provider.SearchAsync(query, default);
        check(handler.Calls == 1 && cached[0].Fields[MetadataField.Description] == "Description", "VNDB cache avoids requests and isolates mutation");
        try { await provider.SearchAsync(new("Other game"), default); throw new Exception("Cooldown ignored"); }
        catch (InvalidOperationException) { check(handler.Calls == 1, "VNDB local cooldown limits requests"); }
        using var badClient = new HttpClient(new Handler { Payload = "{invalid" });
        try { await new VndbMetadataProvider(badClient).SearchAsync(query, default); throw new Exception("Invalid JSON accepted"); }
        catch (JsonException) { check(true, "Malformed VNDB JSON rejected"); }
        var throttled = new Handler { Status = HttpStatusCode.TooManyRequests }; using var throttleClient = new HttpClient(throttled);
        var throttleProvider = new VndbMetadataProvider(throttleClient);
        try { await throttleProvider.SearchAsync(query, default); throw new Exception("429 ignored"); }
        catch (HttpRequestException) { check(true, "VNDB rate limiting reported"); }
        try { await throttleProvider.SearchAsync(query, default); throw new Exception("Backoff ignored"); }
        catch (InvalidOperationException) { check(throttled.Calls == 1, "VNDB rate-limit backoff prevents retry storm"); }
        using var errorClient = new HttpClient(new Handler { Status = HttpStatusCode.ServiceUnavailable });
        try { await new VndbMetadataProvider(errorClient).SearchAsync(query, default); throw new Exception("Failure ignored"); }
        catch (HttpRequestException) { check(true, "Provider failure propagates without library mutation"); }
        using var hugeClient = new HttpClient(new Handler { Payload = new string('x', 2_000_001) });
        try { await new VndbMetadataProvider(hugeClient).SearchAsync(query, default); throw new Exception("Oversize accepted"); }
        catch (InvalidDataException) { check(true, "Oversized network response bounded"); }
        try { await provider.SearchAsync(query, canceled.Token); throw new Exception("Cancellation ignored"); }
        catch (OperationCanceledException) { check(true, "VNDB cancellation precedes cached search"); }
        using var slowClient = new HttpClient(new Handler { Wait = true }); using var cancelDuring = new CancellationTokenSource(50);
        try { await new VndbMetadataProvider(slowClient).SearchAsync(query, cancelDuring.Token); throw new Exception("Cancellation ignored"); }
        catch (OperationCanceledException) { check(true, "VNDB in-flight cancellation"); }
    }
    private static void Reject(Action action, string name, Action<bool, string> check)
    {
        try { action(); throw new Exception("Expected rejection: " + name); }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException) { check(true, name); }
    }
    private sealed class Handler : HttpMessageHandler
    {
        public int Calls; public string Url = "", Body = ""; public bool Wait;
        public HttpStatusCode Status = HttpStatusCode.OK;
        public string Payload = """{"results":[{"id":"v42","title":"Moon Garden","alttitle":null,"aliases":["Lunar Garden"],"released":"2020-03","description":"Description"}]}""";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++; Url = request.RequestUri!.ToString(); Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (Wait) await Task.Delay(Timeout.Infinite, cancellationToken);
            return new(Status) { Content = new StringContent(Payload) };
        }
    }
}
