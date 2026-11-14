using System.Diagnostics;
using System.Text.Json;
using ArcHive.Core;
internal static class V1Tests
{
    public static async Task Run(string root, Action<bool, string> check)
    {
        var dir = Path.Combine(root, "v1"); Directory.CreateDirectory(dir);
        void Reject(Action action, string name) { try { action(); } catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or InvalidDataException or IOException or System.Xml.XmlException) { check(true, name); return; } throw new Exception("Expected rejection: " + name); }
        async Task RejectAsync(Func<Task> action, string name) { try { await action(); } catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or InvalidDataException or IOException or TimeoutException or OperationCanceledException or JsonException or NotSupportedException) { check(true, name); return; } throw new Exception("Expected rejection: " + name); }
        var now = DateTimeOffset.UtcNow; var game = new Game { Title = "Manual Display", Naming = new() { CanonicalTitle = "Canonical" } };
        var session = Playtime.Begin(game, now); Playtime.Tick(session, now.AddSeconds(10), 10);
        check(game.PlaytimeSeconds == 10 && session.Active, "Playtime accumulates observed intervals");
        Reject(() => Playtime.Begin(game, now), "Concurrent sessions for one game rejected");
        Playtime.Tick(session, now.AddHours(1), 3600); check(session.Seconds == 10 && session.NeedsReview, "Sleep and monitoring gaps are not counted as playtime");
        Reject(() => Playtime.Correct(session, 10), "Active session correction rejected");
        check(Playtime.Recover([game]) && !session.Active && session.Seconds == 10, "Interrupted playtime retains checkpoints and requests review");
        check(!Playtime.Recover([game]), "Playtime recovery is idempotent");
        Playtime.Correct(session, 2.5); check(session.Seconds == 150 && !session.NeedsReview, "Manual correction replaces duration without changing start time");
        Reject(() => Playtime.Correct(session, double.NaN), "Non-finite manual playtime rejected");
        Reject(() => Playtime.Correct(session, -1), "Negative playtime rejected");
        var original = Path.Combine(dir, "MyAwesomeGame_v1.2_win64.zip"); File.WriteAllText(original, "archive fixture"); game.Archives.Add(new() { Path = original });
        var naming = NamingReview.Suggest(game); check(naming.SuggestedTitle == "My Awesome Game" && naming.Evidence.Any(e => e.Contains("1.2")) && game.Title == "Manual Display", "Naming preview preserves manual title and records token evidence");
        NamingReview.Apply(game, naming, "Reviewed Canonical", false); check(game.Title == "Manual Display" && game.Naming.Reviewed, "Canonical naming can preserve display title");
        NamingReview.Apply(game, naming, "Chosen Display", true); check(game.Title == "Chosen Display" && File.Exists(original), "Explicit naming application never renames source files");
        game.Collections.Add("Weekend"); game.Tags = "Puzzle, Story"; game.ControllerSupport = ControllerSupport.Full;
        var other = new Game { Title = "Other", Description = "Keep original conflict", Collections = ["Favorites"], IsNsfw = true, Sessions = [new() { Seconds = 60, Started = now }] };
        var folder = Path.Combine(dir, "Installed"); Directory.CreateDirectory(folder); var exe = Path.Combine(folder, "game.exe"); File.WriteAllText(exe, "fixture");
        var archive = new GameArchive { Path = original }; other.Archives.Add(archive);
        var profile = new LaunchProfile { Name = "Manual", ExecutablePath = exe, WorkingDirectory = folder, Arguments = ["--manual"] };
        other.Installations.Add(new() { Path = folder, SourceArchiveId = archive.Id, Profiles = [profile], DefaultProfileId = profile.Id });
        var saves = Path.Combine(dir, "saves"); Directory.CreateDirectory(saves); File.WriteAllText(Path.Combine(saves, "progress.sav"), "progress");
        var location = new SaveLocation { Folder = saves }; other.SaveLocations.Add(location);
        var backupStore = Path.Combine(dir, "backups"); var snapshot = await new SaveBackups(backupStore, other.Id, location).BackupAsync(default);
        var library = new Library { Games = [game, other] }; var before = JsonSerializer.Serialize(library);
        var merge = DuplicateMerge.Preview(library, [game.Id, other.Id], game.Id);
        check(JsonSerializer.Serialize(library) == before, "Merge preview does not mutate the library");
        var merged = DuplicateMerge.Apply(library, merge); var result = merged.Games.Single();
        check(result.Title == game.Title && result.Description == game.Description && result.MergedRecords.Single().Description == other.Description, "Merge primary values win and conflicting source fields remain reviewable");
        check(result.Installations.Single().DefaultProfileId == profile.Id && result.Installations.Single().SourceArchiveId == archive.Id, "Merge preserves launch defaults and source archive IDs");
        check(result.PlaytimeSeconds == 210 && result.Collections.Count == 2 && result.IsNsfw, "Merge combines sessions and collections and preserves NSFW classification");
        check((await new SaveBackups(backupStore, result.Id, result.SaveLocations.Single()).ListAsync()).Count == 1, "Merged game can access original save snapshots through preserved owner IDs");
        File.WriteAllText(Path.Combine(saves,"progress.sav"), "changed");
        var mergedBackups = new SaveBackups(backupStore,result.Id,result.SaveLocations.Single());
        var restore = await mergedBackups.PreviewAsync(snapshot.Id); await mergedBackups.RestoreAsync(restore);
        check(File.ReadAllText(Path.Combine(saves,"progress.sav")) == "progress", "Merged game restores a snapshot created under the original game ID");
        check(File.Exists(original) && File.Exists(exe) && File.ReadAllText(Path.Combine(saves, "progress.sav")) == "progress", "Merge leaves archives, installations and game saves untouched");
        game.Description = "Changed after preview"; Reject(() => DuplicateMerge.Apply(library, merge), "Stale merge previews rejected"); game.Description = "";
        var active = Playtime.Begin(other, now); Reject(() => DuplicateMerge.Preview(library, [game.Id, other.Id], game.Id), "Merging actively tracked games rejected"); Playtime.Finish(active, false);
        var database = Path.Combine(dir, "library.db"); var store = new SqliteLibraryStore(database); store.Load(); store.Save(merged); var loaded = store.Load().Games.Single();
        check(loaded.Sessions.Count == result.Sessions.Count && loaded.Naming.CanonicalTitle == "Chosen Display" && loaded.Collections.Count == 2 && loaded.MergedRecords.Count == 1, "SQLite round-trips all additive v1 library fields");
        var add = BatchOperations.Preview(merged, [result.Id], new(BatchAction.AddCollection, Tags: "Weekend, New"));
        check(add.Result.Games[0].Collections.Count == 3 && merged.Games[0].Collections.Count == 2, "Batch collection preview is distinct and non-mutating");
        var remove = BatchOperations.Preview(add.Result, [result.Id], new(BatchAction.RemoveCollection, Tags: "weekend")); check(!remove.Result.Games[0].Collections.Contains("Weekend"), "Batch collection removal is case insensitive");
        var mark = BatchOperations.Preview(merged, [result.Id], new(BatchAction.MarkPlayed)); check(mark.Result.Games[0].Played && mark.Result.Games[0].Progress == result.Progress, "Batch played flag is independent of Play Status");
        var browse = new BrowseOptions(Collection: "Weekend", Tag: "Puzzle", ControllerOnly: true);
        check(LibraryBrowsing.Apply([game, other], browse).Single().Id == game.Id, "Richer browsing combines collection, tag and controller filters");
        check(!LibraryBrowsing.Apply([game, other], browse with { Engine = "Missing" }).Any(), "Combined browsing filters return an empty result correctly");
        check(LibraryBrowsing.Apply([other, game], new()).First() == other, "Default browse preserves existing settings order");
        var many = Enumerable.Range(0, 10000).Select(i => new Game { Title = "Game " + i, Tags = "Story", Collections = ["All"], Sessions = [new() { Seconds = i }] }).ToArray();
        var clock = Stopwatch.StartNew(); var filtered = LibraryBrowsing.Apply(many, new(Collection: "All", Tag: "Story", Sort: "Playtime")).ToArray(); clock.Stop();
        check(filtered.Length == 10000 && filtered[0].PlaytimeSeconds == 9999, "10,000-game filter and playtime sort produce correct results");
        Console.WriteLine($"BENCHMARK 10,000-game browse: {clock.ElapsedMilliseconds} ms");
        check(LibraryBrowsing.Statistics([game, other]).Games == 2, "Library statistics count the supplied visibility scope");
        using (var queue = new ExtractionQueue(Path.Combine(dir, "queue.json")))
        {
            Reject(() => queue.EnqueueBatch([(game, game.Archives[0]), (game, game.Archives[0])], dir), "Duplicate batch extraction rejected atomically");
            check(queue.Jobs.Count == 0, "Failed batch enqueue publishes no partial jobs");
            queue.EnqueueBatch([(game, game.Archives[0]), (other, archive)], dir); check(queue.Jobs.Count == 2, "Batch extraction queues all reviewed associations");
            var recovery = store.Purge(); check(store.Load().Games.Count == 0 && File.Exists(recovery), "Purge empties the library and keeps a permanent database recovery copy");
            Reject(() => store.CompletePendingPurge(() => throw new IOException("fixture interruption")), "Interrupted purge queue cleanup retains its marker");
            check(store.CompletePendingPurge(queue.ClearForLibraryReset) && queue.Jobs.Count == 0, "Purge recovery clears queue history after restart boundary");
            check(!store.CompletePendingPurge(queue.ClearForLibraryReset), "Purge cleanup is idempotent");
            check(new SqliteLibraryStore(recovery).Load().Games.Count == 1 && File.Exists(original) && Directory.Exists(saves), "Pre-purge backup remains readable and game files remain untouched");
        }
        // File operations use actual fixtures and persisted recovery boundaries.
        var moving = Path.Combine(dir, "OldName"); Directory.CreateDirectory(moving); File.WriteAllText(Path.Combine(moving, "game.exe"), "move fixture");
        var moveGame = new Game { Title = "NewName", Installations = [new() { Path = moving, Profiles = [new() { ExecutablePath = Path.Combine(moving, "game.exe"), WorkingDirectory = moving }] }] };
        var moveLibrary = new Library { Games = [moveGame] }; var journal = Path.Combine(dir, "moves.json");
        var plan = LibraryFileMoves.Preview(moveLibrary, [moveGame.Id], null, true);
        check(Directory.Exists(moving) && !Directory.Exists(plan.Moves[0].Destination), "Rename preview makes no filesystem changes");
        File.AppendAllText(Path.Combine(moving, "game.exe"), " changed"); Reject(() => LibraryFileMoves.Apply(moveLibrary, plan, journal, _ => { }), "Stale source content rejects a file operation");
        plan = LibraryFileMoves.Preview(moveLibrary, [moveGame.Id], null, true);
        Reject(() => LibraryFileMoves.Apply(moveLibrary, plan, journal, _ => throw new IOException("simulated database failure")), "Database failure retains move recovery journal");
        check(File.Exists(journal) && Directory.Exists(plan.Moves[0].Destination), "Interrupted move leaves content and recovery evidence intact");
        LibraryFileMoves.Recover(moveLibrary, journal); check(Directory.Exists(moving) && !Directory.Exists(plan.Moves[0].Destination), "Recovery rolls uncommitted moves back to original paths");
        plan = LibraryFileMoves.Preview(moveLibrary, [moveGame.Id], null, true); Library? saved = null;
        var moved = LibraryFileMoves.Apply(moveLibrary, plan, journal, l => saved = l);
        check(saved == moved && File.Exists(moved.Games[0].Installations[0].Profiles[0].ExecutablePath), "Successful rename rebases launch paths and persists the resulting library");
        File.WriteAllText(journal, JsonSerializer.Serialize(new FileMoveJournal(1, plan))); LibraryFileMoves.Recover(moved, journal);
        check(Directory.Exists(plan.Moves[0].Destination) && !File.Exists(journal), "Recovery recognizes committed moves without rolling them back");
        var target = Path.Combine(dir, "target"); Directory.CreateDirectory(target); Directory.CreateDirectory(Path.Combine(target, "NewName"));
        Reject(() => LibraryFileMoves.Preview(moved, [moveGame.Id], target, false), "Move destination collisions never overwrite files");
        var protectedGame = moved.Games[0]; protectedGame.SaveLocations.Add(new() { Folder = protectedGame.Installations[0].Path }); protectedGame.Title = "Another";
        Reject(() => LibraryFileMoves.Preview(moved, [moveGame.Id], null, true), "Move of configured save folders requires separate backup review");
        var originalNaming = result.Naming.OriginalName; result.Archives[0].Path = Path.Combine(dir, "Renamed.zip");
        check(NamingReview.Suggest(result).OriginalName == originalNaming, "Naming review retains the original name after a path change");
        check(UiText.Availability("Ready") == "Ready to Play" && UiText.Extraction(ExtractionState.Cancelled) == "Canceled" && JsonSerializer.Serialize(ExtractionState.Cancelled) == "6", "Presentation terminology does not change persisted enum values");
        using (var process = Process.Start(new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "ArcHive.Tests.exe")) { UseShellExecute = false, CreateNoWindow = true, ArgumentList = { "--parent-process" } })!)
        {
            var monitor = new ProcessSessionMonitor(process); var deadline = Stopwatch.StartNew();
            while (!monitor.HadChildren && deadline.Elapsed < TimeSpan.FromSeconds(5)) { monitor.IsRunning(); await Task.Delay(30); }
            check(monitor.HadChildren, "Playtime observes a real child process without a mapper or input injection");
            while (!process.HasExited && deadline.Elapsed < TimeSpan.FromSeconds(6)) { monitor.IsRunning(); await Task.Delay(30); }
            check(monitor.IsRunning(), "Playtime continues while an observed child outlives its launcher");
            while (monitor.IsRunning() && deadline.Elapsed < TimeSpan.FromSeconds(8)) await Task.Delay(50);
            check(!monitor.IsRunning(), "Playtime ends when all observed process identities exit");
        }
        var persistedFolder = Path.Combine(dir, "PersistedOld"); Directory.CreateDirectory(persistedFolder); File.WriteAllText(Path.Combine(persistedFolder,"game.exe"), "fixture");
        var persisted = new Library { Games = [new() { Title = "PersistedNew", Installations = [new() { Path = persistedFolder }] }] };
        VersionTracking.InitializeLocations(persisted.Games[0]); var moveStore = new SqliteLibraryStore(Path.Combine(dir,"move.db")); moveStore.Load(); moveStore.Save(persisted); persisted = moveStore.Load();
        var persistedPlan = LibraryFileMoves.Preview(persisted, [persisted.Games[0].Id], null, true);
        LibraryFileMoves.Apply(persisted, persistedPlan, journal, moveStore.Save);
        File.WriteAllText(journal, JsonSerializer.Serialize(new FileMoveJournal(1,persistedPlan)));
        LibraryFileMoves.Recover(moveStore.Load(), journal); check(!File.Exists(journal), "Committed move recovery agrees with actual SQLite round-trip and release history");
        await Integrations(dir, check, RejectAsync);
    }
    private static async Task Integrations(string dir, Action<bool,string> check, Func<Func<Task>,string,Task> reject)
    {
        var le = Path.Combine(dir, "locale"); Directory.CreateDirectory(le);
        foreach (var name in new[] { "LEProc.exe", "LoaderDll.dll", "LocaleEmulator.dll" }) File.WriteAllText(Path.Combine(le,name), "fixture");
        var id = Guid.NewGuid().ToString(); var xml = $"<LEConfig><Profiles><Profile Name='Japanese' Guid='{id}'><Location>ja-JP</Location><Timezone>Tokyo Standard Time</Timezone><Parameter></Parameter></Profile></Profiles></LEConfig>";
        File.WriteAllText(Path.Combine(le,"LEConfig.xml"), xml); var profiles = LocaleEmulatorAdapter.ReadProfiles(Path.Combine(le,"LEProc.exe"));
        check(profiles.Single().CodePage == 932 && profiles.Single().Locale == "ja-JP", "Locale profiles expose culture, timezone and derived code page");
        check(LocaleEmulatorAdapter.Detect([le]).Count == 1, "Locale installation detection validates the complete selected folder");
        var exe = Path.Combine(dir,"x86.exe"); var pe = new byte[512]; pe[0]=0x4d; pe[1]=0x5a; BitConverter.GetBytes(128).CopyTo(pe,60); pe[128]=0x50; pe[129]=0x45; BitConverter.GetBytes((ushort)0x14c).CopyTo(pe,132); BitConverter.GetBytes((ushort)224).CopyTo(pe,148); BitConverter.GetBytes((ushort)0x10b).CopyTo(pe,152); File.WriteAllBytes(exe,pe);
        check(LocaleEmulatorAdapter.IsSupportedTarget(exe), "Locale adapter accepts a native x86 PE fixture");
        var adapter = new LocaleEmulatorAdapter(Path.Combine(le,"LEProc.exe")); var profile = new LaunchProfile { AdapterId = "locale-emulator/"+id };
        var launch = await adapter.PrepareAsync(new(exe,dir,["two words", "quoted\"value"]),profile,default);
        check(launch.Arguments[0] == "-runas" && launch.Arguments[1] == id && launch.Arguments[3] == LaunchService.QuoteArgument("two words"), "Locale launch uses profile ID and preserves argument quoting through LEProc");
        await reject(() => adapter.PrepareAsync(new(exe,le,[]),profile,default), "Locale adapter rejects custom working directory");
        File.WriteAllText(Path.Combine(le,"LEConfig.xml"),xml.Replace("<Parameter></Parameter>","<Parameter>override</Parameter>"));
        await reject(() => adapter.PrepareAsync(new(exe,dir,["argument"]),profile,default), "Locale profile cannot silently override game arguments");
        File.WriteAllText(Path.Combine(le,"LEConfig.xml"),"<!DOCTYPE x [<!ENTITY v SYSTEM 'file:///C:/missing'>]><LEConfig>&v;</LEConfig>");
        try { LocaleEmulatorAdapter.ReadProfiles(Path.Combine(le,"LEProc.exe")); throw new Exception("DTD allowed"); } catch(System.Xml.XmlException) { check(true,"Locale XML prohibits external entities"); }
        var distribution = Path.Combine(dir,"plugin-source"); Directory.CreateDirectory(distribution);
        foreach(var file in Directory.GetFiles(AppContext.BaseDirectory).Where(p => new[]{".exe",".dll",".json"}.Contains(Path.GetExtension(p)))) File.Copy(file,Path.Combine(distribution,Path.GetFileName(file)));
        File.WriteAllText(Path.Combine(distribution,"plugin-fixture.mode"),"fixture");
        var manifest = new PluginManifest(1,"fixture.plugin","Fixture","1.0.0","1.0.0","ArcHive.Tests.exe",["metadata","launch"]);
        File.WriteAllText(Path.Combine(distribution,"plugin.json"),JsonSerializer.Serialize(manifest,PluginCatalog.Json));
        var catalog = new PluginCatalog(Path.Combine(dir,"plugin-data")); var installed = catalog.Install(distribution);
        check(!installed.Enabled && catalog.List().Count == 1, "Explicit plugin installation starts disabled");
        var host = new PluginHost(catalog,TimeSpan.FromSeconds(5));
        await reject(() => host.InvokeAsync(manifest.Id,new(1,"metadata.search",new("ok")),default),"Disabled plugins cannot execute");
        catalog.SetEnabled(manifest.Id,true);
        check((await host.InvokeAsync(manifest.Id,new(1,"metadata.search",new("ok")),default)).Candidates!.Length == 0,"Enabled metadata plugin returns bounded versioned JSON in a separate process");
        await reject(() => host.InvokeAsync(manifest.Id,new(1,"metadata.search",new("bad")),default),"Malformed plugin response rejected");
        await reject(() => host.InvokeAsync(manifest.Id,new(1,"metadata.search",new("oversize")),default),"Oversized plugin output is bounded and rejected");
        await reject(() => host.InvokeAsync(manifest.Id,new(1,"metadata.search",new("crash")),default),"Crashed plugin fails without modifying the library");
        await reject(() => new PluginHost(catalog,TimeSpan.FromMilliseconds(300)).InvokeAsync(manifest.Id,new(1,"metadata.search",new("wait")),default),"Plugin timeout terminates the subprocess");
        using (var cancel = new CancellationTokenSource()) { cancel.Cancel(); await reject(() => host.InvokeAsync(manifest.Id,new(1,"metadata.search",new("ok")),cancel.Token),"Plugin honors pre-cancellation"); }
        var native = new LaunchPlan(exe,dir,["test"]); var pluginPlan = await new PluginLaunchAdapter(catalog,manifest.Id).PrepareAsync(native,new(),default);
        check(pluginPlan.Executable == native.Executable && pluginPlan.Arguments.SequenceEqual(native.Arguments),"Plugin launch adapter preserves a validated native launch plan");
        File.AppendAllText(Path.Combine(dir,"plugin-data","plugins",installed.Folder,"plugin-fixture.mode"),"tampered");
        await reject(() => host.InvokeAsync(manifest.Id,new(1,"metadata.search",new("ok")),default),"Changed plugin distribution fails hash validation before execution");
        catalog.SetEnabled(manifest.Id,false); catalog.Remove(manifest.Id); check(catalog.List().Count == 0 && File.Exists(Path.Combine(distribution,"plugin.json")),"Plugin removal retains the original distribution");
    }
}
