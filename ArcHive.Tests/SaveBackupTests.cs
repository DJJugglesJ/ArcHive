using System.Text.Json;
using ArcHive.Core;
internal static class SaveBackupTests
{
    public static async Task Run(string root, Action<bool, string> check)
    {
        var home = Path.Combine(root, "save-backup-tests"); Directory.CreateDirectory(home);
        var saves = Path.Combine(home, "game", "saves"); Directory.CreateDirectory(Path.Combine(saves, "empty"));
        var storage = Path.Combine(home, "snapshots"); var game = new Game(); var location = new SaveLocation { Folder = saves, Retention = 2 }; game.SaveLocations.Add(location);
        var service = new SaveBackups(storage, game.Id, location);
        void Put(string path, string text) { var full = Path.Combine(saves, path); Directory.CreateDirectory(Path.GetDirectoryName(full)!); File.WriteAllText(full, text); }
        async Task Reject(Func<Task> action, string name) { try { await action(); } catch (Exception ex) when (ex is IOException or InvalidDataException or OperationCanceledException or JsonException) { check(true, name); return; } throw new Exception("Expected rejection: " + name); }
        Put("slot1.sav", "first"); Put("nested/settings.ini", "settings");
        var initial = await service.BackupAsync();
        check(initial.Entries.Count(e => !e.Directory) == 2 && initial.Entries.Any(e => e.Directory && e.Path == "empty"), "Save snapshot includes nested files and empty folders");
        check(File.ReadAllText(Path.Combine(saves, "slot1.sav")) == "first", "Backup preserves live saves");
        Put("slot1.sav", "second"); Put("extra.sav", "extra"); File.Delete(Path.Combine(saves, "nested/settings.ini"));
        var preview = await service.PreviewAsync(initial.Id);
        check(preview.Changes.Any(c => c.StartsWith("Replace: slot1")) && preview.Changes.Any(c => c.StartsWith("Add: nested")) && preview.Changes.Contains("Remove: extra.sav"), "Restore preview lists additions replacements and removals");
        Put("slot1.sav", "changed after preview");
        await Reject(() => service.RestoreAsync(preview), "Stale restore previews are rejected");
        check(File.ReadAllText(Path.Combine(saves, "slot1.sav")) == "changed after preview", "Stale restore leaves current saves untouched");
        preview = await service.PreviewAsync(initial.Id); var restored = await service.RestoreAsync(preview);
        check(File.ReadAllText(Path.Combine(saves, "slot1.sav")) == "first" && !File.Exists(Path.Combine(saves, "extra.sav")) && Directory.Exists(Path.Combine(saves, "empty")), "Restore replaces entire folder including removal and empty directories");
        check(restored.SafetySnapshotId is not null && (await service.ListAsync()).Single(s => s.Id == restored.SafetySnapshotId).Protected, "Restore creates a protected pre-restore snapshot");
        check(restored.PreviousFolder is not null && File.ReadAllText(Path.Combine(restored.PreviousFolder, "slot1.sav")) == "changed after preview", "Restore retains the original folder as additional recovery evidence");
        var undo = await service.PreviewAsync(restored.SafetySnapshotId!.Value); await service.RestoreAsync(undo);
        check(File.ReadAllText(Path.Combine(saves, "extra.sav")) == "extra", "Protected pre-restore snapshot can undo a restore");
        await service.BackupAsync(); await service.BackupAsync();
        var list = await service.ListAsync();
        check(list.Count(s => !s.Protected) == 2 && list.Count(s => s.Protected) == 2, "Retention prunes regular snapshots and preserves all safety snapshots");
        var latest = list.First(s => !s.Protected);
        var scope = Path.Combine(storage, game.Id.ToString("N"), location.Id.ToString("N"));
        var payload = Path.Combine(scope, latest.Id.ToString("N"), "files", "slot1.sav"); File.WriteAllText(payload, "tampered");
        await Reject(() => service.PreviewAsync(latest.Id), "Corrupt snapshot bytes fail integrity validation");
        check(File.ReadAllText(Path.Combine(saves, "slot1.sav")) == "changed after preview", "Corrupt snapshot does not modify saves");
        await Reject(() => new SaveBackups(storage, Guid.NewGuid(), location).PreviewAsync(latest.Id), "Other games cannot select a snapshot by ID");
        await Reject(() => new SaveBackups(storage, game.Id, location, byteLimit: 1).BackupAsync(), "Save backup byte limit enforced");
        await Reject(() => new SaveBackups(storage, game.Id, location, entryLimit: 1).BackupAsync(), "Save backup entry limit enforced");
        using (var cancellation = new CancellationTokenSource()) { cancellation.Cancel(); await Reject(() => service.BackupAsync(cancellation.Token), "Cancelled backup publishes no snapshot"); }
        check((await service.ListAsync()).Count == list.Count, "Failed and cancelled backups are excluded from snapshot listing");
        await Reject(() => new SaveBackups(Path.Combine(saves, "nested-store"), game.Id, location).BackupAsync(), "Nested backup destination is rejected");
        await Reject(() => new SaveBackups(storage, game.Id, new SaveLocation { Folder = storage }).BackupAsync(), "Save folder cannot be backup storage");
        await Reject(() => new SaveBackups(storage, game.Id, new SaveLocation { Folder = Path.GetPathRoot(saves)! }).BackupAsync(), "Drive roots cannot be configured as save folders");
        await Reject(() => new SaveBackups(storage, game.Id, new SaveLocation { Folder = "relative" }).BackupAsync(), "Relative save locations are rejected");
        await Reject(() => new SaveBackups(storage, game.Id, new SaveLocation { Folder = saves, Retention = 0 }).BackupAsync(), "Invalid retention is rejected");
        using (var gate = new FileStream(Path.Combine(scope, "operation.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            await Reject(() => service.BackupAsync(), "Concurrent operations on the same save location are rejected");
        // Simulate a crash after moving the original folder but before publishing staged saves.
        var transaction = Guid.NewGuid(); var previous = Path.Combine(Path.GetDirectoryName(saves)!, ".archive-previous-" + transaction.ToString("N"));
        Directory.Move(saves, previous); File.WriteAllText(Path.Combine(scope, "restore.json"), JsonSerializer.Serialize(new { Id = transaction, Target = saves }));
        await Reject(() => service.BackupAsync(), "Pending restore blocks further backup operations");
        await service.RecoverAsync();
        check(Directory.Exists(saves) && File.Exists(Path.Combine(saves, "slot1.sav")) && !File.Exists(Path.Combine(scope, "restore.json")), "Crash recovery returns the displaced original folder");
        check((await service.RecoverAsync()).StartsWith("No interrupted"), "Recovery is idempotent");
        File.WriteAllText(Path.Combine(scope, "restore.json"), JsonSerializer.Serialize(new { Id = Guid.NewGuid(), Target = home }));
        await Reject(() => service.RecoverAsync(), "Recovery rejects a changed destination"); File.Delete(Path.Combine(scope, "restore.json"));
        var db = new SqliteLibraryStore(Path.Combine(home, "library.db")); db.Load(); db.Save(new Library { Games = [game] }); var loaded = db.Load().Games.Single();
        check(loaded.SaveLocations.Single().Id == location.Id && loaded.SaveLocations.Single().Folder == saves && loaded.SaveLocations.Single().Retention == 2, "Save configuration round-trips through SQLite");
        var legacy = JsonSerializer.Deserialize<Game>("{\"Title\":\"Legacy\"}")!;
        check(legacy.SaveLocations.Count == 0, "Existing libraries default to no configured save folders");
        using (var held = new FileStream(Path.Combine(saves, "slot1.sav"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            await Reject(() => service.BackupAsync(), "Locked game saves fail without publishing incomplete backups");
        var preserved = File.ReadAllText(Path.Combine(saves, "slot1.sav"));
        using (var cancelled = new CancellationTokenSource())
        {
            var plan = await service.PreviewAsync(restored.SafetySnapshotId.Value); cancelled.Cancel();
            await Reject(() => service.RestoreAsync(plan, cancelled.Token), "Cancelled restore leaves original saves in place");
        }
        check(File.ReadAllText(Path.Combine(saves, "slot1.sav")) == preserved, "Cancelled restore preserves current save bytes");
        File.WriteAllText(Path.Combine(scope, "restore.json"), JsonSerializer.Serialize(new { Id = Guid.NewGuid(), Target = saves }));
        await service.RecoverAsync();
        check(File.ReadAllText(Path.Combine(saves, "slot1.sav")) == preserved, "Recovery before a swap keeps the original folder unchanged");
        var emptyFolder = Path.Combine(home, "empty-saves"); Directory.CreateDirectory(emptyFolder); var emptyLocation = new SaveLocation { Folder = emptyFolder };
        var emptyService = new SaveBackups(storage, game.Id, emptyLocation); var emptySnapshot = await emptyService.BackupAsync(); Directory.Delete(emptyFolder);
        var missing = await emptyService.PreviewAsync(emptySnapshot.Id); await emptyService.RestoreAsync(missing);
        check(Directory.Exists(emptyFolder), "Empty snapshot restores a missing save folder");
        Directory.Delete(emptyFolder);
        await Reject(() => emptyService.BackupAsync(), "Missing save folder is not silently backed up as empty");
        Directory.CreateDirectory(emptyFolder);
        var emptyPayload = Path.Combine(storage, game.Id.ToString("N"), emptyLocation.Id.ToString("N"), emptySnapshot.Id.ToString("N"), "files");
        Directory.Delete(emptyPayload);
        await Reject(() => emptyService.PreviewAsync(emptySnapshot.Id), "Missing payload is rejected even for an empty snapshot");
        File.WriteAllText(Path.Combine(emptyFolder, "a.sav"), "a"); File.WriteAllText(Path.Combine(emptyFolder, "b.sav"), "b");
        var boundedLocation = new SaveLocation { Folder = emptyFolder, Retention = 1 };
        var bounded = new SaveBackups(storage, game.Id, boundedLocation, entryLimit: 2);
        await bounded.BackupAsync(); await bounded.BackupAsync();
        check((await bounded.ListAsync()).Count == 1, "Retention handles snapshots at the exact payload entry limit");
        var link = Path.Combine(saves, "linked");
        Directory.CreateSymbolicLink(link, emptyFolder);
        await Reject(() => service.BackupAsync(), "Directory links in save trees are rejected"); Directory.Delete(link);
        var clean = new SaveLocation { Folder = saves }; var cleanService = new SaveBackups(storage, game.Id, clean); var cleanSnapshot = await cleanService.BackupAsync();
        var cleanRoot = Path.Combine(storage, game.Id.ToString("N"), clean.Id.ToString("N"), cleanSnapshot.Id.ToString("N"));
        var manifestPath = Path.Combine(cleanRoot, "snapshot.json"); var originalManifest = File.ReadAllText(manifestPath);
        var hostile = cleanSnapshot with { Entries = [new("../escape.sav", false, 1, "bad")] }; File.WriteAllText(manifestPath, JsonSerializer.Serialize(hostile));
        await Reject(() => cleanService.PreviewAsync(cleanSnapshot.Id), "Manipulated traversal manifest fails validation");
        File.WriteAllText(manifestPath, originalManifest);
        var validPreview = await cleanService.PreviewAsync(cleanSnapshot.Id); File.WriteAllText(Path.Combine(cleanRoot, "files", "slot1.sav"), "changed");
        await Reject(() => cleanService.RestoreAsync(validPreview), "Snapshot modified after preview is rejected at restore");
    }
}
