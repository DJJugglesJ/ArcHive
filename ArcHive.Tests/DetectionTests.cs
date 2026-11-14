using System.Text.Json;
using ArcHive.Core;
internal static class DetectionTests
{
    public static async Task Run(string root, Action<bool, string> check)
    {
        var folder = Path.Combine(root, "detect", "MoonGarden", "123"); Directory.CreateDirectory(folder);
        void Touch(string path) { var full = Path.Combine(folder, path.Replace('/', Path.DirectorySeparatorChar)); Directory.CreateDirectory(Path.GetDirectoryName(full)!); File.WriteAllText(full, "fixture only"); }
        Touch("MoonGarden.exe"); Touch("UnityPlayer.dll"); Directory.CreateDirectory(Path.Combine(folder, "MoonGarden_Data"));
        Touch("UnityCrashHandler64.exe"); Touch("unins000.exe"); Touch("setup.exe"); Touch("tools/editor.exe"); Touch("_CommonRedist/vcredist.exe");
        var profile = new LaunchProfile { Name = "My manual launch", ExecutablePath = Path.Combine(folder, "MoonGarden.exe"), Arguments = ["--literal", "two words"], WorkingDirectory = folder };
        var installation = new GameInstallation { Path = folder, Profiles = [profile], DefaultProfileId = profile.Id };
        var game = new Game { Title = "Manual title", Engine = "Manual engine", Installations = [installation] };
        var library = new Library { Games = [game] }; var before = JsonSerializer.Serialize(library);
        var report = await LaunchDetection.ScanAsync(installation);
        check(report.Candidates.Count == 1 && report.Candidates[0].Path.EndsWith("MoonGarden.exe"), "Detection excludes uninstallers, crash helpers and support-folder tools");
        check(report.Candidates[0].Engine == "Unity" && report.Candidates[0].EvidenceText.Contains("MoonGarden_Data") && report.Candidates[0].EvidenceText.Contains("UnityPlayer.dll"), "Unity suggestions expose paired runtime/data-path evidence");
        check(report.Notes.Any(n => n.Contains("unins000")) && report.Notes.Any(n => n.Contains("tools")), "Excluded launch candidates explain their exclusion");
        check(JsonSerializer.Serialize(library) == before, "Scanning preserves manual titles, engines and profiles");
        var unchanged = LaunchDetection.Apply(library, game.Id, report, null, null, null);
        check(unchanged.Games[0].Engine == "Manual engine" && unchanged.Games[0].DetectionHistory.Count == 0, "Unchecked suggestions apply no engine or title changes");
        var applied = LaunchDetection.Apply(library, game.Id, report, report.Candidates[0].Path, "Unity", null);
        var saved = applied.Games[0];
        check(saved.Title == "Manual title" && saved.Engine == "Unity", "Only explicitly chosen engine changes replace manual values");
        check(saved.Installations[0].Profiles.Count == 1 && saved.Installations[0].Profiles[0].Arguments.SequenceEqual(profile.Arguments) && saved.Installations[0].DefaultProfileId == profile.Id, "Selecting an existing executable preserves manual arguments and default profile");
        check(saved.DetectionHistory.Single().Evidence.Any(e => e.Contains("UnityPlayer")) && JsonSerializer.Serialize(library) == before, "Accepted detection evidence is recorded on a copy without changing the source");
        Touch("Alternative.exe"); var multiple = await LaunchDetection.ScanAsync(installation);
        check(multiple.Candidates.Count == 2 && multiple.Candidates[0].Engine == "Unity" && multiple.Notes.Any(n => n.StartsWith("Multiple launch")), "Strong evidence ranks above unknown candidates without selecting automatically");
        var alt = multiple.Candidates.Single(c => c.Engine == "Unknown");
        var alternate = LaunchDetection.Apply(library, game.Id, multiple, alt.Path, null, null).Games[0].Installations[0];
        check(alternate.Profiles.Count == 2 && alternate.DefaultProfileId == profile.Id && alternate.Profiles[0].Name == profile.Name, "New candidate adds a profile without replacing an existing profile or default");
        var blankGame = new Game { Installations = [new() { Path = folder }] }; var blankLibrary = new Library { Games = [blankGame] };
        var blankReport = await LaunchDetection.ScanAsync(blankGame.Installations[0]);
        var first = LaunchDetection.Apply(blankLibrary, blankGame.Id, blankReport, alt.Path, null, null).Games[0].Installations[0];
        check(first.Profiles.Count == 1 && first.DefaultProfileId == first.Profiles[0].Id, "First accepted candidate creates a usable default profile");
        File.Delete(alt.Path);
        try { LaunchDetection.Apply(library, game.Id, multiple, alt.Path, null, null); throw new Exception("Missing candidate accepted"); }
        catch (FileNotFoundException) { check(JsonSerializer.Serialize(library) == before, "Disappearing executable prevents apply without partial edits"); }
        check(FilenameParser.FromLocation(folder, true).Title == "Moon Garden · 123", "Numeric folder names retain identifiers with immediate parent context");
        check(FilenameParser.FromLocation(Path.Combine(folder, "1984.zip"), false).Title == "1984", "Numeric parents do not invent a title context");
        var renamed = LaunchDetection.Apply(library, game.Id, report, null, null, "Moon Garden · 123");
        check(renamed.Games[0].Title == "Moon Garden · 123" && game.Title == "Manual title", "Existing numeric/source title correction requires explicit approval");
        var scan = Scanner.Scan(Path.Combine(root, "detect"), default);
        check(scan.Games.Any(g => g.Title == "Moon Garden · 123"), "New discovery supplies context for numeric-only folder entries");
        Scanner.Merge(library, scan.Games);
        check(library.Games.First(g => g.Id == game.Id).Title == "Manual title", "Rescan preserves an existing manually named installation");
        check(game.SourceSummary.Contains(folder) && game.SourceLocation == folder, "Source locations remain available independently of display title");
        async Task Engine(string sub, string engine, params string[] markers)
        {
            foreach (var marker in markers) Touch(sub + "/" + marker);
            var i = new GameInstallation { Path = Path.Combine(folder, sub) }; var result = await LaunchDetection.ScanAsync(i);
            check(result.Engines.Any(e => e.Name == engine && e.Evidence.Length > 0), engine + " uses observed files as evidence");
        }
        await Engine("Renpy", "Ren'Py", "Story.exe", "renpy/runtime.py", "game/script.rpy");
        await Engine("MV", "RPG Maker MV", "Game.exe", "www/js/rpg_core.js");
        await Engine("MZ", "RPG Maker MZ", "Game.exe", "js/rmmz_core.js");
        await Engine("Godot", "Godot (possible)", "Game.exe", "Game.pck");
        await Engine("Unreal", "Unreal Engine (possible)", "Project/Binaries/Win64/Project-Shipping.exe");
        check((await LaunchDetection.ScanAsync(installation)).Notes.Any(n => n.StartsWith("Multiple engine")), "Conflicting engine signatures are explicitly flagged");
        check((await LaunchDetection.ScanAsync(installation, entryLimit: 1)).Notes.Any(n => n.Contains("limit")), "Detection bounds entries and reports incomplete results");
        check((await LaunchDetection.ScanAsync(installation, depthLimit: 0)).Notes.Any(n => n.Contains("Depth limit")), "Detection bounds recursive depth");
        using (var cancel = new CancellationTokenSource())
        {
            cancel.Cancel(); try { await LaunchDetection.ScanAsync(installation, cancel.Token); throw new Exception("Cancellation ignored"); }
            catch (OperationCanceledException) { check(true, "Detection supports cancellation"); }
        }
        var db = new SqliteLibraryStore(Path.Combine(root, "detection.db")); db.Load(); db.Save(applied);
        check(db.Load().Games[0].DetectionHistory[0].Evidence.SequenceEqual(saved.DetectionHistory[0].Evidence), "Accepted evidence survives a SQLite restart");
        var reducer = new GamepadNavigation(); reducer.Update(0, 0, 0, true, TimeSpan.Zero);
        check(reducer.Update(PadButtons.Options, 0, 0, true, TimeSpan.FromMilliseconds(1)) == PadAction.Options, "Controller north button opens options");
        check(reducer.Update(PadButtons.Options, 0, 0, true, TimeSpan.FromSeconds(1)) == PadAction.None, "Holding Triangle cannot repeatedly open or activate options");
        var ds4 = new byte[64]; ds4[0] = 1; ds4[1] = ds4[2] = 128; ds4[5] = 0x88;
        check(PlayStationInput.TryDecode(0x054c, 0x05c4, ds4, out var pad) && pad.Buttons == PadButtons.Options, "DS4 Triangle reports map to options");
        var ds5 = new byte[64]; ds5[0] = 1; ds5[1] = ds5[2] = 128; ds5[8] = 0x88;
        check(PlayStationInput.TryDecode(0x054c, 0x0ce6, ds5, out pad) && pad.Buttons == PadButtons.Options, "DualSense Triangle reports map to options");
        check(DisplayPaths.Compact(@"D:\Downloads\Games\MoonGarden\123") == @"D:\…\MoonGarden\123", "Display paths keep drive plus immediate parent and game folder");
        check(DisplayPaths.Compact(@"D:\Games\MoonGarden") == @"D:\Games\MoonGarden", "Short paths do not get unnecessary ellipses");
        check(DisplayPaths.Compact(@"D:\") == @"D:\", "Drive roots retain their separator");
        check(DisplayPaths.Compact(@"\\server\share\downloads\games\Title") == @"\\server\share\…\games\Title", "UNC display paths retain server/share identity");
        check(DisplayPaths.Compact(@"D:/Downloads/Games/Title/") == @"D:\…\Games\Title", "Display paths normalize slashes and trailing separators");
        check(DisplayPaths.Compact(null) == "No source linked", "Missing source displays an explicit placeholder");
        check(game.CompactSource.Contains("…") && game.SourceLocation == folder, "Compact source presentation never rewrites stored paths");
        var input = new GamepadNavigation(); var details = false; var launches = 0;
        input.Update(0, 0, 0, true, TimeSpan.Zero);
        void Dispatch(PadAction action) { if (action != PadAction.Accept) return; if (LibraryInteraction.Accept(details) == LibraryAcceptAction.OpenDetails) details = true; else launches++; }
        Dispatch(input.Update(PadButtons.A, 0, 0, true, TimeSpan.FromMilliseconds(10)));
        check(details && launches == 0, "Cross from collection opens details without launching");
        Dispatch(input.Update(PadButtons.A, 0, 0, true, TimeSpan.FromSeconds(1)));
        check(launches == 0, "Held Cross cannot spill from opening details into Play");
        Dispatch(input.Update(0, 0, 0, true, TimeSpan.FromSeconds(2)));
        Dispatch(input.Update(PadButtons.A, 0, 0, true, TimeSpan.FromSeconds(3)));
        check(launches == 1, "Play requires a fresh accept press on the details page");
    }
}
