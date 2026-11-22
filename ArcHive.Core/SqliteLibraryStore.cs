using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace ArcHive.Core;

public sealed class SqliteLibraryStore(string database, string? legacyJson = null)
{
    private SqliteConnection Open()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(database))!);
        var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database, ForeignKeys = true, Pooling = false }.ToString());
        c.Open(); return c;
    }
    private static void Execute(SqliteConnection c, SqliteTransaction? tx, string sql, params object?[] values)
    {
        using var command = c.CreateCommand(); command.Transaction = tx; command.CommandText = sql;
        for (var i = 0; i < values.Length; i++) command.Parameters.AddWithValue("$p" + i, values[i] ?? DBNull.Value);
        command.ExecuteNonQuery();
    }
    private static int Schema(SqliteConnection c)
    { using var command = c.CreateCommand(); command.CommandText = "PRAGMA user_version"; return Convert.ToInt32(command.ExecuteScalar()); }
    private static void Backup(SqliteConnection c, string destination)
    {
        using var copy = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = destination, Pooling = false }.ToString());
        copy.Open(); c.BackupDatabase(copy);
    }
    public Library Load()
    {
        using var c = Open(); var schema = Schema(c);
        if (schema is < 0 or > 2) throw new InvalidDataException("Unsupported SQLite library version. No data was changed.");
        if (schema < 2)
        {
            var imported = schema == 1 ? Read(c, 1)
                : legacyJson is not null && File.Exists(legacyJson) ? new LibraryStore(legacyJson).Load() : new Library();
            foreach (var game in imported.Games) VersionTracking.InitializeLocations(game, migratePlayed: true);
            if (schema == 1) Backup(c, database + ".pre-v2-" + Guid.NewGuid().ToString("N") + ".db");
            else if (legacyJson is not null && File.Exists(legacyJson))
                File.Copy(legacyJson, legacyJson + ".pre-sqlite-" + Guid.NewGuid().ToString("N") + ".bak");
            using var tx = c.BeginTransaction();
            if (schema == 0) Execute(c, tx, """
                CREATE TABLE games(id TEXT PRIMARY KEY, metadata TEXT NOT NULL);
                CREATE TABLE roots(path TEXT PRIMARY KEY);
                CREATE TABLE archives(id TEXT PRIMARY KEY, game_id TEXT NOT NULL REFERENCES games(id) ON DELETE CASCADE, path TEXT NOT NULL);
                CREATE TABLE installations(id TEXT PRIMARY KEY, game_id TEXT NOT NULL REFERENCES games(id) ON DELETE CASCADE, path TEXT NOT NULL, executable TEXT, source_archive_id TEXT REFERENCES archives(id));
                CREATE INDEX archives_game ON archives(game_id);
                CREATE INDEX installations_game ON installations(game_id);
                """);
            Execute(c, tx, """
                ALTER TABLE archives ADD COLUMN release TEXT NOT NULL DEFAULT '{}';
                ALTER TABLE installations ADD COLUMN release TEXT NOT NULL DEFAULT '{}';
                ALTER TABLE installations ADD COLUMN default_profile_id TEXT;
                CREATE TABLE launch_profiles(id TEXT PRIMARY KEY, installation_id TEXT NOT NULL REFERENCES installations(id) ON DELETE CASCADE,
                    name TEXT NOT NULL, executable TEXT NOT NULL, arguments_json TEXT NOT NULL, working_directory TEXT NOT NULL, adapter_id TEXT);
                CREATE INDEX profiles_installation ON launch_profiles(installation_id);
                """);
            Write(c, tx, imported); Execute(c, tx, "PRAGMA user_version=2"); tx.Commit();
        }
        return Read(c, 2);
    }
    private static Library Read(SqliteConnection c, int schema)
    {
        var result = new Library();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT metadata FROM games ORDER BY rowid";
            using var r = cmd.ExecuteReader();
            while (r.Read()) result.Games.Add(JsonSerializer.Deserialize<Game>(r.GetString(0)) ?? throw new InvalidDataException("Invalid game metadata."));
        }
        var games = result.Games.ToDictionary(g => g.Id.ToString());
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT id,game_id,path" + (schema == 2 ? ",release" : "") + " FROM archives ORDER BY rowid";
            using var r = cmd.ExecuteReader();
            while (r.Read()) games[r.GetString(1)].Archives.Add(new() { Id = Guid.Parse(r.GetString(0)), Path = r.GetString(2),
                Release = schema == 2 ? JsonSerializer.Deserialize<ReleaseInfo>(r.GetString(3)) ?? new() : new() });
        }
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT id,game_id,path,executable,source_archive_id" + (schema == 2 ? ",release,default_profile_id" : "") + " FROM installations ORDER BY rowid";
            using var r = cmd.ExecuteReader();
            while (r.Read()) games[r.GetString(1)].Installations.Add(new() { Id = Guid.Parse(r.GetString(0)), Path = r.GetString(2),
                ExecutablePath = r.IsDBNull(3) ? null : r.GetString(3), SourceArchiveId = r.IsDBNull(4) ? null : Guid.Parse(r.GetString(4)),
                Release = schema == 2 ? JsonSerializer.Deserialize<ReleaseInfo>(r.GetString(5)) ?? new() : new(),
                DefaultProfileId = schema == 2 && !r.IsDBNull(6) ? Guid.Parse(r.GetString(6)) : null });
        }
        if (schema == 2)
        {
            var installations = result.Games.SelectMany(g => g.Installations).ToDictionary(i => i.Id.ToString());
            using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT id,installation_id,name,executable,arguments_json,working_directory,adapter_id FROM launch_profiles ORDER BY rowid";
            using var r = cmd.ExecuteReader();
            while (r.Read()) installations[r.GetString(1)].Profiles.Add(new() { Id = Guid.Parse(r.GetString(0)), Name = r.GetString(2),
                ExecutablePath = r.GetString(3), Arguments = JsonSerializer.Deserialize<List<string>>(r.GetString(4)) ?? [],
                WorkingDirectory = r.GetString(5), AdapterId = r.IsDBNull(6) ? null : r.GetString(6) });
        }
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT path FROM roots ORDER BY rowid";
            using var r = cmd.ExecuteReader(); while (r.Read()) result.Roots.Add(r.GetString(0));
        }
        return result;
    }
    public void Save(Library library)
    {
        using var c = Open();
        if (Schema(c) != 2) throw new InvalidOperationException("Load the supported library before saving.");
        Backup(c, database + ".bak");
        using var tx = c.BeginTransaction(); Write(c, tx, library); tx.Commit();
    }
    public string Purge()
    {
        using var c = Open();
        if (Schema(c) != 2) throw new InvalidOperationException("Load the supported library before purging it.");
        var backup = database + ".pre-purge-" + Guid.NewGuid().ToString("N") + ".db";
        Backup(c, backup);
        using var tx = c.BeginTransaction();
        Execute(c, tx, "CREATE TABLE IF NOT EXISTS maintenance(key TEXT PRIMARY KEY, value TEXT NOT NULL)");
        Write(c, tx, new Library());
        Execute(c, tx, "INSERT OR REPLACE INTO maintenance VALUES('purge_pending',$p0)", backup);
        tx.Commit(); return backup;
    }
    public bool CompletePendingPurge(Action clearQueue)
    {
        using var c = Open();
        using var command = c.CreateCommand();
        command.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='maintenance'";
        if (Convert.ToInt32(command.ExecuteScalar()) == 0) return false;
        command.CommandText = "SELECT count(*) FROM maintenance WHERE key='purge_pending'";
        if (Convert.ToInt32(command.ExecuteScalar()) == 0) return false;
        clearQueue();
        Execute(c, null, "DELETE FROM maintenance WHERE key='purge_pending'"); return true;
    }
    private static void Write(SqliteConnection c, SqliteTransaction tx, Library library)
    {
        Execute(c, tx, "DELETE FROM launch_profiles; DELETE FROM installations; DELETE FROM archives; DELETE FROM games; DELETE FROM roots;");
        foreach (var root in library.Roots.Distinct(StringComparer.OrdinalIgnoreCase)) Execute(c, tx, "INSERT INTO roots VALUES($p0)", root);
        foreach (var g in library.Games)
        {
            g.UpgradeLocations();
            if (!Enum.IsDefined(g.ControllerSupport)) throw new InvalidDataException("Unknown controller support.");
            if (!Enum.IsDefined(g.Progress)) throw new InvalidDataException("Unknown progress status.");
            foreach (var archive in g.Archives) VersionTracking.Record(g, archive.Release, "Archive " + archive.Id);
            foreach (var installation in g.Installations) VersionTracking.Record(g, installation.Release, "Installation " + installation.Id);
            VersionTracking.Record(g, g.LatestRelease, g.LatestReleaseSource);
            var metadata = new Game { PersonalReview = g.PersonalReview, Sessions = g.Sessions, Naming = g.Naming, Collections = g.Collections, MergedRecords = g.MergedRecords, F95Thread = g.F95Thread, OwnedLibraryIds = g.OwnedLibraryIds, SaveLocations = g.SaveLocations, DetectionHistory = g.DetectionHistory, IsNsfw = g.IsNsfw, Artwork = g.Artwork, ControllerSupport = g.ControllerSupport, Metadata = g.Metadata, ReleaseYear = g.ReleaseYear, Engine = g.Engine, Id = g.Id, Title = g.Title, Tags = g.Tags, Description = g.Description, Favorite = g.Favorite,
                Played = g.Played, LastLaunched = g.LastLaunched, ScreenshotPath = g.ScreenshotPath, Progress = g.Progress,
                LatestRelease = g.LatestRelease, LatestReleaseSource = g.LatestReleaseSource, LatestReleaseCheckedAt = g.LatestReleaseCheckedAt, ReleaseHistory = g.ReleaseHistory };
            Execute(c, tx, "INSERT INTO games VALUES($p0,$p1)", g.Id.ToString(), JsonSerializer.Serialize(metadata));
            foreach (var a in g.Archives) Execute(c, tx, "INSERT INTO archives VALUES($p0,$p1,$p2,$p3)", a.Id.ToString(), g.Id.ToString(), a.Path, JsonSerializer.Serialize(a.Release));
            foreach (var i in g.Installations)
            {
                if (i.SourceArchiveId is Guid source && !g.Archives.Any(a => a.Id == source)) throw new InvalidDataException("Installation source must belong to the same game.");
                if (i.DefaultProfileId is Guid profileId && !i.Profiles.Any(p => p.Id == profileId)) throw new InvalidDataException("Default profile must belong to its installation.");
                Execute(c, tx, "INSERT INTO installations VALUES($p0,$p1,$p2,$p3,$p4,$p5,$p6)", i.Id.ToString(), g.Id.ToString(), i.Path, i.ExecutablePath, i.SourceArchiveId?.ToString(), JsonSerializer.Serialize(i.Release), i.DefaultProfileId?.ToString());
                foreach (var p in i.Profiles)
                    Execute(c, tx, "INSERT INTO launch_profiles VALUES($p0,$p1,$p2,$p3,$p4,$p5,$p6)", p.Id.ToString(), i.Id.ToString(), p.Name, p.ExecutablePath, JsonSerializer.Serialize(p.Arguments), p.WorkingDirectory, p.AdapterId);
            }
        }
    }
}
