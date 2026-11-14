using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace GameShelf.Core;

public sealed class SqliteLibraryStore(string database, string? legacyJson = null)
{
    private SqliteConnection Open()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(database))!);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database, ForeignKeys = true, Pooling = false }.ToString());
        connection.Open();
        return connection;
    }
    private static void Execute(SqliteConnection c, SqliteTransaction? tx, string sql, params object?[] values)
    {
        using var command = c.CreateCommand(); command.Transaction = tx; command.CommandText = sql;
        for (var i = 0; i < values.Length; i++) command.Parameters.AddWithValue("$p" + i, values[i] ?? DBNull.Value);
        command.ExecuteNonQuery();
    }
    public Library Load()
    {
        using var c = Open();
        using var version = c.CreateCommand(); version.CommandText = "PRAGMA user_version";
        var schema = Convert.ToInt32(version.ExecuteScalar());
        if (schema != 0 && schema != 1) throw new InvalidDataException("Unsupported SQLite library version. No data was changed.");
        if (schema == 0)
        {
            // Parse and back up before committing any schema or imported data.
            var imported = legacyJson is not null && File.Exists(legacyJson) ? new LibraryStore(legacyJson).Load() : new Library();
            foreach (var g in imported.Games) g.UpgradeLocations();
            if (legacyJson is not null && File.Exists(legacyJson))
                File.Copy(legacyJson, legacyJson + ".pre-sqlite-" + Guid.NewGuid().ToString("N") + ".bak");
            using var tx = c.BeginTransaction();
            Execute(c, tx, """
                CREATE TABLE games(id TEXT PRIMARY KEY, metadata TEXT NOT NULL);
                CREATE TABLE roots(path TEXT PRIMARY KEY);
                CREATE TABLE archives(id TEXT PRIMARY KEY, game_id TEXT NOT NULL REFERENCES games(id) ON DELETE CASCADE, path TEXT NOT NULL);
                CREATE TABLE installations(id TEXT PRIMARY KEY, game_id TEXT NOT NULL REFERENCES games(id) ON DELETE CASCADE, path TEXT NOT NULL, executable TEXT, source_archive_id TEXT REFERENCES archives(id));
                CREATE INDEX archives_game ON archives(game_id);
                CREATE INDEX installations_game ON installations(game_id);
                """);
            Write(c, tx, imported);
            Execute(c, tx, "PRAGMA user_version=1");
            tx.Commit();
        }
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
            cmd.CommandText = "SELECT id,game_id,path FROM archives ORDER BY rowid";
            using var r = cmd.ExecuteReader();
            while (r.Read()) games[r.GetString(1)].Archives.Add(new() { Id = Guid.Parse(r.GetString(0)), Path = r.GetString(2) });
        }
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT id,game_id,path,executable,source_archive_id FROM installations ORDER BY rowid";
            using var r = cmd.ExecuteReader();
            while (r.Read()) games[r.GetString(1)].Installations.Add(new() { Id = Guid.Parse(r.GetString(0)), Path = r.GetString(2), ExecutablePath = r.IsDBNull(3) ? null : r.GetString(3), SourceArchiveId = r.IsDBNull(4) ? null : Guid.Parse(r.GetString(4)) });
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
        using var version = c.CreateCommand(); version.CommandText = "PRAGMA user_version";
        if (Convert.ToInt32(version.ExecuteScalar()) != 1) throw new InvalidOperationException("Load the supported library before saving.");
        using (var backup = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database + ".bak", Pooling = false }.ToString()))
        { backup.Open(); c.BackupDatabase(backup); }
        using var tx = c.BeginTransaction();
        Write(c, tx, library); tx.Commit();
    }
    private static void Write(SqliteConnection c, SqliteTransaction tx, Library library)
    {
        Execute(c, tx, "DELETE FROM installations; DELETE FROM archives; DELETE FROM games; DELETE FROM roots;");
        foreach (var root in library.Roots.Distinct(StringComparer.OrdinalIgnoreCase)) Execute(c, tx, "INSERT INTO roots VALUES($p0)", root);
        foreach (var g in library.Games)
        {
            g.UpgradeLocations();
            var metadata = new Game { Id = g.Id, Title = g.Title, Tags = g.Tags, Description = g.Description, Favorite = g.Favorite, Played = g.Played, LastLaunched = g.LastLaunched, ScreenshotPath = g.ScreenshotPath };
            Execute(c, tx, "INSERT INTO games VALUES($p0,$p1)", g.Id.ToString(), JsonSerializer.Serialize(metadata));
            foreach (var a in g.Archives) Execute(c, tx, "INSERT INTO archives VALUES($p0,$p1,$p2)", a.Id.ToString(), g.Id.ToString(), a.Path);
            foreach (var i in g.Installations)
            {
                if (i.SourceArchiveId is Guid source && !g.Archives.Any(a => a.Id == source)) throw new InvalidDataException("Installation source must belong to the same game.");
                Execute(c, tx, "INSERT INTO installations VALUES($p0,$p1,$p2,$p3,$p4)", i.Id.ToString(), g.Id.ToString(), i.Path, i.ExecutablePath, i.SourceArchiveId?.ToString());
            }
        }
    }
}
