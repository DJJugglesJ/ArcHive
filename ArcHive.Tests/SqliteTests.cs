using ArcHive.Core;
using Microsoft.Data.Sqlite;
using System.Text.Json;

static class SqliteTests
{
    public static void Run(string root, Action<bool, string> check)
    {
        var legacy = Path.Combine(root, "migration.json");
        var db = Path.Combine(root, "migration.db");
        var game = new Game { Title = "雪 ' game", ArchivePath = Path.Combine(root, "old.rar"), DirectoryPath = Path.Combine(root, "installed"), ExecutablePath = Path.Combine(root, "installed", "game.exe"), Description = "notes\nsecond line", Tags = "puzzle, favorite", Favorite = true, Played = true, ScreenshotPath = "cover.jpg", LastLaunched = DateTimeOffset.UtcNow };
        var original = new Library { Roots = [root], Games = [game] };
        new LibraryStore(legacy).Save(original);
        var originalBytes = File.ReadAllBytes(legacy);
        var repository = new SqliteLibraryStore(db, legacy);
        var imported = repository.Load(); var g = imported.Games.Single();
        check(g.Id == game.Id && g.Title == game.Title && g.Description == game.Description && g.Tags == game.Tags && g.Favorite && g.Played && g.ScreenshotPath == game.ScreenshotPath && g.LastLaunched == game.LastLaunched, "SQLite import preserves all metadata");
        check(g.Archives.Single().Path == game.ArchivePath && g.Installations.Single().Path == game.DirectoryPath && g.Installations.Single().ExecutablePath == game.ExecutablePath && imported.Roots.SequenceEqual(original.Roots), "SQLite imports paths, launch setting and roots");
        check(File.ReadAllBytes(legacy).SequenceEqual(originalBytes) && Directory.GetFiles(root, "migration.json.pre-sqlite-*.bak").Length == 1, "Legacy source and migration backup retained");
        g.Archives.Add(new() { Path = Path.Combine(root, "new.7z") });
        g.Installations.Add(new() { Path = Path.Combine(root, "second"), ExecutablePath = Path.Combine(root, "second", "different.exe"), SourceArchiveId = g.Archives[1].Id });
        repository.Save(imported);
        var roundTrip = repository.Load().Games.Single();
        check(roundTrip.Archives.Count == 2 && roundTrip.Installations.Count == 2 && roundTrip.Installations[1].SourceArchiveId == g.Archives[1].Id && roundTrip.Installations[1].ExecutablePath == g.Installations[1].ExecutablePath, "Multiple locations and per-folder executable round trip");
        File.WriteAllText(legacy, "invalid now");
        check(repository.Load().Games.Single().Archives.Count == 2, "Migration is not repeated after SQLite initialization");
        var count = imported.Games.Count;
        Scanner.Merge(imported, [new Game { ArchivePath = g.Archives[1].Path }, new Game { DirectoryPath = Path.Combine(g.Installations[1].Path, "nested") }]);
        check(imported.Games.Count == count, "Rescan recognizes secondary archive and folder");
        imported.Games.Add(g);
        try { repository.Save(imported); throw new Exception("Duplicate ID accepted"); }
        catch (SqliteException) { check(repository.Load().Games.Count == 1, "Failed save rolls back the whole transaction"); }
        check(new SqliteLibraryStore(db + ".bak").Load().Games.Count == 1, "SQLite backup is readable");
        var invalid = Path.Combine(root, "bad-migration.json"); File.WriteAllText(invalid, "broken");
        var badStore = new SqliteLibraryStore(Path.Combine(root, "bad-migration.db"), invalid);
        try { badStore.Load(); throw new Exception("Corrupt import accepted"); }
        catch (JsonException) { check(File.ReadAllText(invalid) == "broken", "Corrupt import leaves source unchanged"); }
        new LibraryStore(invalid).Save(new Library());
        check(badStore.Load().Games.Count == 0, "Failed migration can be retried");
    }
}
