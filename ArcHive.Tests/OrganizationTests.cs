using System.Text.Json;
using ArcHive.Core;

internal static class OrganizationTests
{
    private sealed class ProgressAction(Action<string> action) : IProgress<string> { public void Report(string value) => action(value); }
    public static async Task Run(string root, Action<bool, string> check)
    {
        var folder = Path.Combine(root, "organization"); Directory.CreateDirectory(folder);
        string FileAt(string name, string contents) { var path = Path.Combine(folder, name); File.WriteAllText(path, contents); return path; }
        var aPath = FileAt("original.zip", "same archive bytes"); var bPath = FileAt("copy.7z", "same archive bytes");
        var cPath = FileAt("different.zip", "different content!");
        var a = new Game { Title = "MoonGarden_v1.0_win64", Tags = "Puzzle, Story", Favorite = true, Archives = [new() { Path = aPath }] };
        var b = new Game { Title = "MoonGarden_v2.0_win64", Archives = [new() { Path = bPath }] };
        var c = new Game { Title = "Unrelated", IsNsfw = true, Archives = [new() { Path = cPath }] };
        var shared = new Game { Title = "Another title", Archives = [new() { Path = aPath.ToUpperInvariant() }] };
        a.Installations.Add(new() { Path = folder, SourceArchiveId = a.Archives[0].Id,
            Profiles = [new() { Name = "Custom", ExecutablePath = Path.Combine(folder, "game.exe"), Arguments = ["--label", "snow garden"], WorkingDirectory = folder }] });
        a.Installations[0].DefaultProfileId = a.Installations[0].Profiles[0].Id;
        var library = new Library { Roots = [folder], Games = [a, b, c, shared] };
        var original = JsonSerializer.Serialize(library);
        var quick = await DuplicateDetection.ScanAsync(library.Games, false);
        check(quick.Groups.Count(g => g.Kind == DuplicateKind.SharedPath) == 1, "Duplicate path comparison is Windows case-insensitive");
        check(quick.Groups.Single(g => g.Kind == DuplicateKind.PossibleTitle).Members.Select(m => m.GameId).ToHashSet().SetEquals([a.Id, b.Id]), "Explicit version/platform tokens yield a possible-title group, not exact-copy evidence");
        check(!quick.Groups.Any(g => g.Kind == DuplicateKind.ExactArchive), "Quick duplicate review never claims matching archive content");
        var hashed = await DuplicateDetection.ScanAsync(library.Games, true);
        var exact = hashed.Groups.Single(g => g.Kind == DuplicateKind.ExactArchive);
        check(exact.Members.Select(m => m.GameId).ToHashSet().SetEquals([a.Id, b.Id, shared.Id]), "SHA-256 finds identical bytes across names/extensions while retaining all associations");
        check(!exact.Members.Any(m => m.GameId == c.Id), "Equal-size different archive content does not match");
        check(JsonSerializer.Serialize(library) == original, "Duplicate scans do not mutate library metadata or paths");
        var visible = library.Games.Where(g => new AppPreferences().Includes(g));
        check(!(await DuplicateDetection.ScanAsync(visible, true)).Groups.SelectMany(g => g.Members).Any(m => m.GameId == c.Id), "Duplicate review respects the supplied visible scope");
        check((await DuplicateDetection.ScanAsync([], true)).Groups.Count == 0, "Empty duplicate scope returns no groups");
        var broken = new Game { Title = "Missing", Archives = [new() { Path = Path.Combine(folder, "missing.zip") }, new() { Path = "relative.zip" }] };
        check((await DuplicateDetection.ScanAsync([a, b, broken], true)).Warnings.Count == 2, "Missing and invalid paths are reported without aborting usable matches");
        using (var cancel = new CancellationTokenSource())
        {
            cancel.Cancel();
            try { await DuplicateDetection.ScanAsync(library.Games, true, token: cancel.Token); throw new Exception("Cancellation ignored"); }
            catch (OperationCanceledException) { check(true, "Duplicate scan honors pre-cancellation"); }
        }
        using (var cancel = new CancellationTokenSource())
        {
            try { await DuplicateDetection.ScanAsync([a, b], true, new ProgressAction(_ => cancel.Cancel()), cancel.Token); throw new Exception("Hash cancellation ignored"); }
            catch (OperationCanceledException) { check(File.ReadAllText(aPath) == "same archive bytes", "Hash cancellation preserves original files"); }
        }
        var mutating = FileAt("changing.zip", "same archive bytes"); var changed = new Game { Title = "Changing", Archives = [new() { Path = mutating }] };
        var race = await DuplicateDetection.ScanAsync([a, changed], true, new ProgressAction(s => { if (s.EndsWith(mutating)) File.AppendAllText(mutating, "changed"); }));
        check(race.Warnings.Count == 1 && !race.Groups.Any(g => g.Kind == DuplicateKind.ExactArchive), "Archive changed between size prefilter and open is skipped");
        using (var locked = new FileStream(bPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var report = await DuplicateDetection.ScanAsync([a, b], true);
            check(report.Warnings.Count == 1 && !report.Groups.Any(g => g.Kind == DuplicateKind.ExactArchive), "Locked archives produce per-file warnings instead of false matches");
        }
        var oneAssociation = await DuplicateDetection.ScanAsync([a, shared], true);
        check(!oneAssociation.Groups.Any(g => g.Kind == DuplicateKind.ExactArchive), "Two references to one path are not two independent byte copies");
        var sameFolder = new Game { Title = "Same installed folder", Installations = [new() { Path = folder + Path.DirectorySeparatorChar }] };
        check((await DuplicateDetection.ScanAsync([a, sameFolder], false)).Groups.Any(g => g.Kind == DuplicateKind.SharedPath), "Installation path grouping normalizes trailing separators");
        var blank = await DuplicateDetection.ScanAsync([new Game(), new Game()], false);
        check(blank.Groups.Count == 0, "Empty titles never produce duplicate candidates");

        BatchPlan Plan(BatchAction action, string tags = "", GameProgress progress = GameProgress.Backlog) => BatchOperations.Preview(library, [a.Id, b.Id, a.Id], new(action, progress, tags));
        var favorite = Plan(BatchAction.Favorite);
        check(favorite.Changes.Count == 2 && favorite.Result.Games.Take(2).All(g => g.Favorite), "Batch deduplicates selection and updates each requested entry");
        check(!favorite.Result.Games[2].Favorite && !b.Favorite, "Batch preview leaves unselected entries and live source unchanged");
        check(!Plan(BatchAction.Unfavorite).Result.Games[0].Favorite, "Batch can remove favorites");
        var status = Plan(BatchAction.SetStatus, progress: GameProgress.Completed);
        check(status.Result.Games.Take(2).All(g => g.Progress == GameProgress.Completed) && !status.Result.Games[0].Played, "Batch status does not rewrite played history");
        var tagged = Plan(BatchAction.AddTags, " puzzle, Co-op, co-OP, , 雪 ");
        check(tagged.Result.Games[0].Tags == "Puzzle, Story, Co-op, 雪", "Batch tags trim and deduplicate without losing existing order or Unicode");
        check(tagged.Result.Games[0].Metadata.Fields[MetadataField.Tags].Provider == "Manual", "Batch tag edits retain manual field provenance");
        check(Plan(BatchAction.RemoveTags, "PUZZLE").Result.Games[0].Tags == "Story", "Batch tag removal matches whole tags case-insensitively");
        check(Plan(BatchAction.RemoveTags, "Puzz").Result.Games[0].Tags == a.Tags, "Batch removal never removes substring matches");
        check(Plan(BatchAction.MarkNsfw).Result.Games.Take(2).All(g => g.IsNsfw), "Batch marks selected NSFW entries");
        check(!BatchOperations.Preview(library, [c.Id], new(BatchAction.ClearNsfw)).Result.Games[2].IsNsfw, "Batch clears selected NSFW classification");
        var remove = Plan(BatchAction.RemoveEntries);
        check(remove.Result.Games.Select(g => g.Id).SequenceEqual([c.Id, shared.Id]) && remove.Result.Roots.SequenceEqual(library.Roots), "Batch forget removes only selected records and retains scan roots");
        check(File.Exists(aPath) && File.Exists(bPath) && Directory.Exists(folder), "Removing entries never removes archives or installations");
        var single = BatchOperations.Preview(library, [a.Id], new(BatchAction.RemoveEntries));
        check(single.Changes.Count == 1 && single.Result.Games.All(g => g.Id != a.Id) && single.Result.Games.Count == library.Games.Count-1, "Single-entry removal preserves every other game");
        a.Sessions.Add(new PlaySession { Active = true });
        try { BatchOperations.Preview(library, [a.Id], new(BatchAction.RemoveEntries)); throw new Exception("Active game removal was allowed"); }
        catch (InvalidOperationException) { check(library.Games.Contains(a) && a.Sessions[0].Active, "Active session blocks removal without modifying the library"); }
        a.Sessions.Clear();
        check(JsonSerializer.Serialize(library) == original, "Every batch preview preserves the original library");
        check(JsonSerializer.Serialize(tagged.Result.Games[0].Installations) == JsonSerializer.Serialize(a.Installations) && tagged.Result.Games[0].Archives[0].Id == a.Archives[0].Id, "Batch retains launch arguments, profile IDs, archive links and location metadata");
        void Reject(IEnumerable<Guid> ids, BatchRequest request, string name)
        {
            try { BatchOperations.Preview(library, ids, request); throw new Exception("Invalid batch accepted"); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { check(JsonSerializer.Serialize(library) == original, name); }
        }
        Reject([], new(BatchAction.Favorite), "Empty batch selection is rejected without mutation");
        Reject([a.Id, Guid.NewGuid()], new(BatchAction.Favorite), "Stale batch IDs reject the whole preview");
        Reject([a.Id], new((BatchAction)999), "Unknown batch action rejected");
        Reject([a.Id], new(BatchAction.SetStatus, (GameProgress)999), "Unknown batch status rejected");
        Reject([a.Id], new(BatchAction.AddTags, Tags: ", ,"), "Blank tag batch rejected");
        Reject([a.Id], new(BatchAction.AddTags, Tags: new string('x', 4097)), "Excessive tag input rejected");
        var store = new SqliteLibraryStore(Path.Combine(folder, "batch.db")); store.Load(); store.Save(library);
        store.Save(tagged.Result); var saved = store.Load();
        check(saved.Games[0].Tags == tagged.Result.Games[0].Tags && saved.Games[1].Tags == tagged.Result.Games[1].Tags, "All batch tag results survive SQLite restart");
        check(saved.Games[0].Installations[0].Profiles[0].Arguments.SequenceEqual(a.Installations[0].Profiles[0].Arguments), "Batch save preserves launch profile arguments through SQLite");
        var invalid = BatchOperations.Preview(saved, [a.Id, b.Id], new(BatchAction.SetStatus, GameProgress.Abandoned));
        invalid.Result.Games[1].Installations.Add(new() { Path = folder, SourceArchiveId = Guid.NewGuid() });
        try { store.Save(invalid.Result); throw new Exception("Broken batch save accepted"); }
        catch (InvalidDataException) { check(store.Load().Games.Take(2).All(g => g.Progress == GameProgress.Backlog), "Failure mid-save rolls back every batch change"); }
        store.Save(remove.Result);
        check(store.Load().Games.Count == 2 && new SqliteLibraryStore(Path.Combine(folder, "batch.db.bak")).Load().Games.Count == 4, "Batch removal persists with a readable pre-change database backup");
        check(File.ReadAllText(aPath) == "same archive bytes" && File.ReadAllText(bPath) == "same archive bytes", "Database batch operations leave original archive bytes intact");
        TestDialog(library, check);
    }
    private static void TestDialog(Library library, Action<bool, string> check)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var saveAttempts = 0;
                var dialog = new ArcHive.App.OrganizationWindow(library, library.Games.Where(g => !g.IsNsfw), _ => { saveAttempts++; throw new IOException("Test save failure"); });
                T Get<T>(string name) => (T)dialog.FindName(name);
                var entries = Get<System.Windows.Controls.DataGrid>("Entries");
                var action = Get<System.Windows.Controls.ComboBox>("Action");
                var preview = Get<System.Windows.Controls.Button>("PreviewButton");
                var apply = Get<System.Windows.Controls.Button>("ApplyButton");
                void Click(System.Windows.Controls.Button button) => button.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                check(entries.Items.Count == 3 && !apply.IsEnabled, "Organization dialog begins with visible scope and no applicable preview");
                Click(preview);
                check(!apply.IsEnabled && Get<System.Windows.Controls.DataGrid>("Changes").Items.Count == 0, "Dialog refuses preview without a selection");
                entries.SelectedItems.Add(library.Games[0]); entries.SelectedItems.Add(library.Games[1]); Click(preview);
                check(apply.IsEnabled && Get<System.Windows.Controls.DataGrid>("Changes").Items.Count == 2, "Dialog previews exactly the multi-selected entries");
                entries.SelectedItems.Remove(library.Games[1]);
                check(!apply.IsEnabled, "Changing selection invalidates a prior batch preview");
                action.SelectedIndex = 3;
                var tags = Get<System.Windows.Controls.TextBox>("TagInput"); tags.Text = "new tag"; Click(preview); tags.Text = "different tag";
                check(!apply.IsEnabled, "Editing tag arguments invalidates a prior preview");
                Click(preview); action.SelectedIndex = 2;
                check(!apply.IsEnabled && Get<System.Windows.Controls.ComboBox>("ProgressStatus").Visibility == System.Windows.Visibility.Visible && tags.Visibility == System.Windows.Visibility.Collapsed, "Changing action invalidates preview and shows relevant controls");
                Click(preview); Get<System.Windows.Controls.ComboBox>("ProgressStatus").SelectedItem = GameProgress.Completed;
                check(!apply.IsEnabled, "Changing requested progress invalidates preview");
                var before = JsonSerializer.Serialize(library); Click(preview); Click(apply);
                check(saveAttempts == 1 && JsonSerializer.Serialize(library) == before && Get<System.Windows.Controls.TextBlock>("Summary").Text.Contains("Nothing applied"), "Dialog save failure retains live state and reports the failure");
                dialog.Close();
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(15))) throw new TimeoutException("Organization dialog test timed out.");
        if (failure is not null) throw new Exception("Organization dialog component test failed", failure);
    }

}
