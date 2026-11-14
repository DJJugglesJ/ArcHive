using ArcHive.Core;
using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.Text.Json;

static class MilestoneTests
{
    public static void Run(string root, Action<bool, string> check)
    {
        void Reject(Action action, string name)
        {
            try { action(); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or NotSupportedException) { check(true, name); return; }
            throw new Exception("Expected rejection: " + name);
        }
        var folder = Path.Combine(root, "MilestoneGame_v1.2_Win64"); Directory.CreateDirectory(folder);
        var exe = Path.Combine(folder, "game.exe"); File.WriteAllText(exe, "inert file, never executed");
        var archive = Path.Combine(root, "MilestoneGame_v1.3_Win64.zip"); File.WriteAllText(archive, "index fixture");
        var game = new Game { Title = "Manual title", Played = true, Description = "Preserve notes", Favorite = true, Tags = "manual" };
        var a = new GameArchive { Path = archive };
        var i = new GameInstallation { Path = folder, ExecutablePath = exe };
        // Construct an actual schema-1 database with metadata that predates Progress.
        var database = Path.Combine(root, "schema-one.db");
        using (var c = new SqliteConnection("Data Source=" + database + ";Pooling=False"))
        {
            c.Open(); using var command = c.CreateCommand();
            command.CommandText = """
                CREATE TABLE games(id TEXT PRIMARY KEY, metadata TEXT NOT NULL);
                CREATE TABLE roots(path TEXT PRIMARY KEY);
                CREATE TABLE archives(id TEXT PRIMARY KEY, game_id TEXT NOT NULL REFERENCES games(id), path TEXT NOT NULL);
                CREATE TABLE installations(id TEXT PRIMARY KEY, game_id TEXT NOT NULL REFERENCES games(id), path TEXT NOT NULL, executable TEXT, source_archive_id TEXT REFERENCES archives(id));
                PRAGMA user_version=1;
                INSERT INTO games VALUES($id,$metadata);
                INSERT INTO archives VALUES($archiveId,$id,$archive);
                INSERT INTO installations VALUES($installationId,$id,$folder,$exe,NULL);
                INSERT INTO roots VALUES($folder);
                """;
            command.Parameters.AddWithValue("$id", game.Id.ToString());
            command.Parameters.AddWithValue("$metadata", JsonSerializer.Serialize(new { game.Id, game.Title, game.Played, game.Description, game.Favorite, game.Tags }));
            command.Parameters.AddWithValue("$archiveId", a.Id.ToString()); command.Parameters.AddWithValue("$archive", archive);
            command.Parameters.AddWithValue("$installationId", i.Id.ToString()); command.Parameters.AddWithValue("$folder", folder); command.Parameters.AddWithValue("$exe", exe);
            command.ExecuteNonQuery();
        }
        var store = new SqliteLibraryStore(database);
        var library = store.Load(); game = library.Games.Single(); i = game.Installations.Single(); a = game.Archives.Single();
        check(game.Progress == GameProgress.Playing && game.Played && game.Description == "Preserve notes" && game.Title == "Manual title" && game.Favorite && game.Tags == "manual", "Schema 1 migration preserves metadata and played progress");
        check(a.Release.Version == "1.3" && i.Release.Version == "1.2" && a.Release.OriginalName == Path.GetFileName(archive), "Migration preserves separate archived and installed releases");
        check(i.Profiles.Count == 1 && i.Profiles[0].ExecutablePath == exe && i.DefaultProfileId == i.Profiles[0].Id, "Existing executable becomes default launch profile");
        var backup = Directory.GetFiles(root, "schema-one.db.pre-v2-*.db").Single();
        using (var c = new SqliteConnection("Data Source=" + backup + ";Pooling=False")) { c.Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "PRAGMA user_version"; check(Convert.ToInt32(cmd.ExecuteScalar()) == 1, "Permanent pre-migration backup remains schema 1"); }
        check(game.UpdateState == "Update available" && game.State == "Ready", "Newer local archive and launch-ready installation");
        game.Progress = GameProgress.Completed;
        var profile = new LaunchProfile { Name = "Alternate", ExecutablePath = "game.exe", WorkingDirectory = ".", Arguments = ["--name", "two words", "雪", "", "literal & text", "quote\"", "trailing\\"] };
        i.Profiles.Add(profile); i.DefaultProfileId = profile.Id;
        game.LatestRelease = new() { Version = "v1.4.0", Platform = "Windows x64" };
        game.LatestReleaseSource = "Manual release note"; game.LatestReleaseCheckedAt = DateTimeOffset.UtcNow;
        VersionTracking.Record(game, game.LatestRelease, game.LatestReleaseSource);
        VersionTracking.Record(game, game.LatestRelease, game.LatestReleaseSource);
        check(game.ReleaseHistory.Count == 3, "Release observations deduplicate unchanged values");
        store.Save(library);
        game = store.Load().Games.Single(); i = game.Installations.Single(); profile = i.Profiles.Single(p => p.Id == profile.Id);
        check(game.Progress == GameProgress.Completed && game.Played && game.State == "Ready", "Progress status persists independently of played and availability");
        check(i.Profiles.Count == 2 && i.DefaultProfileId == profile.Id && profile.Arguments.SequenceEqual(new[] { "--name", "two words", "雪", "", "literal & text", "quote\"", "trailing\\" }), "Multiple profiles and exact arguments round-trip");
        check(game.LatestRelease.Version == "v1.4.0" && game.LatestReleaseSource == "Manual release note" && game.LatestReleaseCheckedAt.HasValue && game.ReleaseHistory.Count == 3, "Latest release provenance timestamp and history round-trip");
        check(Directory.GetFiles(root, "schema-one.db.pre-v2-*.db").Length == 1, "Schema migration runs only once");
        var plan = LaunchService.Prepare(i, profile);
        check(plan.Executable == exe && Game.SamePath(plan.WorkingDirectory, folder) && !plan.ToStartInfo().UseShellExecute && plan.ToStartInfo().ArgumentList.SequenceEqual(profile.Arguments), "Launch plan resolves relative paths and uses ArgumentList without shell");
        check(LaunchService.QuoteArgument("") == "\"\"" && LaunchService.QuoteArgument("two words") == "\"two words\"" && LaunchService.QuoteArgument("end\\") == "\"end\\\\\"", "Command preview quotes empty spaced and trailing-backslash arguments");
        profile.ExecutablePath = "../outside.exe"; Reject(() => LaunchService.Prepare(i, profile), "Reject escaping executable");
        profile.ExecutablePath = "game.exe"; profile.WorkingDirectory = ".."; Reject(() => LaunchService.Prepare(i, profile), "Reject escaping working directory");
        profile.WorkingDirectory = "missing"; Reject(() => LaunchService.Prepare(i, profile), "Reject missing working directory");
        profile.WorkingDirectory = "."; profile.AdapterId = "uninstalled"; Reject(() => LaunchService.Prepare(i, profile), "Reject unavailable launch adapter");
        profile.AdapterId = null; profile.Arguments = ["bad\0argument"]; Reject(() => LaunchService.Prepare(i, profile), "Reject null argument characters");
        profile.Arguments = []; profile.ExecutablePath = "missing.exe"; Reject(() => LaunchService.Prepare(i, profile), "Reject missing executable");
        profile.ExecutablePath = "game.exe";
        Reject(() => LaunchService.Prepare(i, new LaunchProfile { ExecutablePath = exe }), "Reject another installation's profile");
        var release = new ReleaseInfo { Version = "1.9", Platform = "Windows x64" };
        check(VersionTracking.Compare(release with { Version = "v1.10" }, release) > 0, "Numeric version ordering handles 1.10 versus 1.9");
        check(VersionTracking.Compare(release with { Version = "1.9.0" }, release) == 0, "Equivalent zero-padded versions");
        check(VersionTracking.Compare(release with { Version = "1.9b" }, release) is null, "Ambiguous suffix versions remain unknown");
        check(VersionTracking.Compare(release with { Channel = "beta" }, release) is null && VersionTracking.Compare(release with { Platform = "Linux" }, release) is null, "Different channels and platforms are incomparable");
        check(VersionTracking.Compare(release with { Version = "" }, release) is null, "Missing version is unknown");
        game.LatestRelease = release with { Version = "1.4" };
        game.Installations.Add(new() { Path = folder, Release = release with { Version = "1.4" } });
        check(game.UpdateState == "Up to date", "Side-by-side newest installation prevents false update flag");
        game.LatestRelease = release with { Version = "1.5beta" };
        check(game.UpdateState == "Unknown", "Uncomparable latest release is not reported up to date");
        // A rescan cannot replace a manually edited title/version/status/profile.
        game.Installations.RemoveAt(1); game.Archives[0].Release.Version = "manual-release";
        library.Games = [game]; Scanner.Merge(library, Scanner.Scan(root, default).Games);
        check(game.Archives[0].Release.Version == "manual-release" && game.Progress == GameProgress.Completed && i.Profiles.Count == 2, "Rescan preserves version overrides progress and profiles");
        library.Games = [game];
        store.Save(library); i.DefaultProfileId = Guid.NewGuid();
        Reject(() => store.Save(library), "Reject default profile owned by another installation");
        check(store.Load().Games.Single().Installations.Single().Profiles.Count == 2, "Invalid profile save rolls back all changes");
        i.DefaultProfileId = null; i.Profiles.Clear(); store.Save(library);
        check(store.Load().Games.Single().Installations.Single().Profiles.Count == 0, "Removing all profiles does not recreate legacy launch choices");
        var clean = new Game(); VersionTracking.InitializeLocations(clean, true);
        check(clean.Progress == GameProgress.Backlog, "Unplayed legacy games remain backlog");
        var explicitStatus = new Game { Played = true, Progress = GameProgress.Abandoned };
        VersionTracking.InitializeLocations(explicitStatus, true);
        check(explicitStatus.Progress == GameProgress.Abandoned, "Explicit progress survives import");
        var failedMigration = Path.Combine(root, "failed-v2.db"); File.Copy(backup, failedMigration);
        using (var c = new SqliteConnection("Data Source=" + failedMigration + ";Pooling=False"))
        {
            c.Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "CREATE TABLE launch_profiles(blocker TEXT)"; cmd.ExecuteNonQuery();
        }
        try { new SqliteLibraryStore(failedMigration).Load(); throw new Exception("Conflicting schema accepted"); }
        catch (SqliteException)
        {
            using var c = new SqliteConnection("Data Source=" + failedMigration + ";Pooling=False"); c.Open();
            using var cmd = c.CreateCommand(); cmd.CommandText = "PRAGMA user_version";
            check(Convert.ToInt32(cmd.ExecuteScalar()) == 1, "Failed migration retains schema version");
            cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('archives')";
            check(Convert.ToInt32(cmd.ExecuteScalar()) == 3, "Failed migration rolls back added columns");
            cmd.CommandText = "SELECT COUNT(*) FROM games";
            check(Convert.ToInt32(cmd.ExecuteScalar()) == 1, "Failed migration preserves library records");
        }
        var future = Path.Combine(root, "future.db"); File.Copy(backup, future);
        using (var c = new SqliteConnection("Data Source=" + future + ";Pooling=False"))
        { c.Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "PRAGMA user_version=99"; cmd.ExecuteNonQuery(); }
        Reject(() => new SqliteLibraryStore(future).Load(), "Future schema fails closed");
        var probe = new GameInstallation { Path = AppContext.BaseDirectory };
        var probeProfile = new LaunchProfile { ExecutablePath = "ArcHive.Tests.exe", WorkingDirectory = ".",
            Arguments = ["--launch-probe", Path.Combine(root, "launch-probe.json"), "two words", "雪", "", "quote\"", "trailing\\", "&literal"] };
        probe.Profiles.Add(probeProfile);
        using (var process = Process.Start(LaunchService.Prepare(probe, probeProfile).ToStartInfo())!)
        {
            if (!process.WaitForExit(10000)) { process.Kill(); throw new Exception("Controlled launch probe timed out"); }
            check(process.ExitCode == 0, "Controlled child-process launch succeeds");
        }
        using var observed = JsonDocument.Parse(File.ReadAllText(probeProfile.Arguments[1]));
        check(observed.RootElement.GetProperty("Arguments").EnumerateArray().Select(a => a.GetString()).SequenceEqual(probeProfile.Arguments.Skip(2)) &&
            Game.SamePath(observed.RootElement.GetProperty("WorkingDirectory").GetString()!, probe.Path), "Launched process receives exact arguments and working directory");
    }
}
