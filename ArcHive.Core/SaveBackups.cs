using System.Security.Cryptography;
using System.Text.Json;

namespace ArcHive.Core;

public sealed class SaveLocation
{
    public Guid? BackupOwnerId { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Folder { get; set; } = "";
    public int Retention { get; set; } = 10;
    public override string ToString() => DisplayPaths.Compact(Folder);
}
public sealed record SaveEntry(string Path, bool Directory, long Size, string Hash);
public sealed record SaveSnapshot(Guid Id, Guid GameId, Guid LocationId, string Source, DateTimeOffset Created, bool Protected, List<SaveEntry> Entries)
{
    public override string ToString() => $"{Created.ToLocalTime():yyyy-MM-dd HH:mm:ss} Â· {Entries.Count(e => !e.Directory)} files" + (Protected ? " Â· Before restore (protected)" : "");
}
public sealed record SaveRestorePreview(Guid SnapshotId, string CurrentFingerprint, string SnapshotFingerprint, string Destination, IReadOnlyList<string> Changes);
public sealed record SaveRestoreResult(Guid? SafetySnapshotId, string? PreviousFolder);

/// <summary>Local, manually requested folder snapshots. Never discovers save paths or follows links.</summary>
public sealed class SaveBackups(string storage, Guid gameId, SaveLocation location, long byteLimit = 2L * 1024 * 1024 * 1024, int entryLimit = 10000)
{
    private string Root => Path.Combine(Path.GetFullPath(storage), (location.BackupOwnerId ?? gameId).ToString("N"), location.Id.ToString("N"));
    private string Target => Path.TrimEndingDirectorySeparator(Path.GetFullPath(location.Folder));
    private string SnapshotPath(Guid id) => Path.Combine(Root, id.ToString("N"));
    private string Journal => Path.Combine(Root, "restore.json");
    private sealed record RestoreJournal(Guid Id, string Target);
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static void ValidateLocation(string folder, string storage)
    {
        if (!Path.IsPathFullyQualified(folder)) throw new IOException("Choose an absolute save folder.");
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        PathSafety.NoLinks(full); PathSafety.NoLinks(storage);
        if (full == Path.GetPathRoot(full) || PathSafety.IsWithin(full, storage) || PathSafety.IsWithin(storage, full))
            throw new IOException("Save and backup folders must be separate; drive roots cannot be backed up.");
        foreach (var special in new[] { Environment.SpecialFolder.UserProfile, Environment.SpecialFolder.Windows, Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolder.ApplicationData, Environment.SpecialFolder.MyDocuments })
        {
            var protectedPath = Environment.GetFolderPath(special);
            if (protectedPath.Length > 0 && PathSafety.IsWithin(full, protectedPath)) throw new IOException("Choose a game's save subfolder, not a system or user-data root.");
        }
        if (File.Exists(full)) throw new IOException("Choose a folder, not a file.");
    }
    private FileStream Lock()
    {
        ValidateLocation(location.Folder, storage);
        if (location.Retention is < 1 or > 100) throw new InvalidDataException("Keep between 1 and 100 regular snapshots.");
        PathSafety.NoLinks(Root); Directory.CreateDirectory(Root); PathSafety.NoLinks(Path.Combine(Root, "operation.lock"));
        return new FileStream(Path.Combine(Root, "operation.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
    private void NoPendingRestore()
    { if (File.Exists(Journal)) throw new IOException("An interrupted restore needs recovery. Choose Recover interrupted restore first."); }
    private List<SaveEntry> Inventory(string folder, CancellationToken token, int allowance = 0)
    {
        PathSafety.NoLinks(folder);
        var entries = new List<SaveEntry>(); long total = 0;
        if (!Directory.Exists(folder)) return entries;
        var pending = new Stack<string>(); pending.Push(folder);
        while (pending.Count > 0)
        {
            foreach (var item in Directory.EnumerateFileSystemEntries(pending.Pop()))
            {
                token.ThrowIfCancellationRequested(); PathSafety.NoLinks(item);
                if (entries.Count >= entryLimit + allowance) throw new IOException("Save folder exceeds the entry limit.");
                var relative = Path.GetRelativePath(folder, item);
                if (relative.Split(Path.DirectorySeparatorChar).Length > 64) throw new IOException("Save folder nesting is too deep.");
                if (Directory.Exists(item)) { entries.Add(new(relative, true, 0, "")); pending.Push(item); }
                else
                {
                    using var input = new FileStream(item, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (input.Length > byteLimit - total) throw new IOException("Save folder exceeds the backup size limit.");
                    total += input.Length;
                    entries.Add(new(relative, false, input.Length, Convert.ToHexString(SHA256.HashData(input))));
                }
            }
        }
        return entries.OrderBy(e => e.Path, StringComparer.Ordinal).ToList();
    }
    private static string Fingerprint(List<SaveEntry> entries) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(entries)));
    private string Current(CancellationToken token) => Directory.Exists(Target) ? "present:" + Fingerprint(Inventory(Target, token)) : "missing";
    private void CopyTree(string source, string destination, List<SaveEntry> entries, CancellationToken token)
    {
        PathSafety.NoLinks(source); PathSafety.NoLinks(destination); Directory.CreateDirectory(destination);
        foreach (var entry in entries)
        {
            token.ThrowIfCancellationRequested();
            var from = Path.GetFullPath(Path.Combine(source, entry.Path)); var to = Path.GetFullPath(Path.Combine(destination, entry.Path));
            if (!PathSafety.IsWithin(source, from) || !PathSafety.IsWithin(destination, to) || entry.Path.Contains(':') || Path.IsPathRooted(entry.Path)) throw new InvalidDataException("Unsafe snapshot path.");
            PathSafety.NoLinks(from); PathSafety.NoLinks(to);
            if (entry.Directory) Directory.CreateDirectory(to);
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                using (var input = new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var output = new FileStream(to, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[81920]; long copied = 0; int read;
                    while ((read = input.Read(buffer)) > 0) { token.ThrowIfCancellationRequested(); copied += read; if (copied > entry.Size) throw new IOException("Save changed during backup."); output.Write(buffer, 0, read); }
                    output.Flush(true);
                }
                File.SetLastWriteTimeUtc(to, File.GetLastWriteTimeUtc(from));
            }
        }
    }
    private static void WriteNew<T>(string path, T value)
    { using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); JsonSerializer.Serialize(file, value, Json); file.Flush(true); }
    private SaveSnapshot Read(Guid id, bool verify, CancellationToken token)
    {
        var folder = SnapshotPath(id); var manifest = Path.Combine(folder, "snapshot.json"); PathSafety.NoLinks(manifest);
        if (new FileInfo(manifest).Length > 16 * 1024 * 1024) throw new InvalidDataException("Oversized snapshot manifest.");
        var snapshot = JsonSerializer.Deserialize<SaveSnapshot>(File.ReadAllText(manifest)) ?? throw new InvalidDataException("Invalid snapshot.");
        if (snapshot.Id != id || snapshot.GameId != (location.BackupOwnerId ?? gameId) || snapshot.LocationId != location.Id || !Game.SamePath(snapshot.Source, Target) || snapshot.Entries is null || snapshot.Entries.Count > entryLimit)
            throw new InvalidDataException("Snapshot does not belong to this save location.");
        if (!Directory.Exists(Path.Combine(folder, "files"))) throw new InvalidDataException("Snapshot payload is missing.");
        if (verify && Fingerprint(Inventory(Path.Combine(folder, "files"), token)) != Fingerprint(snapshot.Entries)) throw new InvalidDataException("Snapshot integrity check failed.");
        return snapshot;
    }
    private List<SaveSnapshot> ListInternal() => Directory.EnumerateDirectories(Root).Where(p => Guid.TryParseExact(Path.GetFileName(p), "N", out _))
        .Select(p => Read(Guid.Parse(Path.GetFileName(p)), false, default)).OrderByDescending(s => s.Created).ToList();
    public Task<List<SaveSnapshot>> ListAsync() => Task.Run(() => { using var gate = Lock(); return ListInternal(); });
    public Task<SaveSnapshot> BackupAsync(CancellationToken token = default) => Task.Run(() => { using var gate = Lock(); NoPendingRestore(); return Backup(false, token); }, token);
    private SaveSnapshot Backup(bool protect, CancellationToken token)
    {
        if (!Directory.Exists(Target)) throw new DirectoryNotFoundException("Save folder does not exist yet.");
        var before = Inventory(Target, token); var id = Guid.NewGuid(); var staging = Path.Combine(Root, ".partial-" + id.ToString("N"));
        var snapshot = new SaveSnapshot(id, location.BackupOwnerId ?? gameId, location.Id, Target, DateTimeOffset.UtcNow, protect, before);
        CopyTree(Target, Path.Combine(staging, "files"), before, token);
        if (Fingerprint(before) != Fingerprint(Inventory(Path.Combine(staging, "files"), token)) || Fingerprint(before) != Fingerprint(Inventory(Target, token)))
            throw new IOException("Saves changed while copying; no snapshot was published. Close the game and try again.");
        WriteNew(Path.Combine(staging, "snapshot.json"), snapshot); token.ThrowIfCancellationRequested(); Directory.Move(staging, SnapshotPath(id));
        // Retention removes only verified complete regular snapshots; safety snapshots are never auto-pruned.
        foreach (var old in ListInternal().Where(s => !s.Protected).Skip(location.Retention))
        { Read(old.Id, true, token); Inventory(SnapshotPath(old.Id), token, 2); Directory.Delete(SnapshotPath(old.Id), true); }
        return snapshot;
    }
    public Task<SaveRestorePreview> PreviewAsync(Guid id, CancellationToken token = default) => Task.Run(() =>
    {
        using var gate = Lock(); NoPendingRestore(); var snapshot = Read(id, true, token); var current = Inventory(Target, token);
        var old = current.ToDictionary(e => e.Path, StringComparer.OrdinalIgnoreCase); var next = snapshot.Entries.ToDictionary(e => e.Path, StringComparer.OrdinalIgnoreCase);
        var changes = next.Values.Where(e => !old.TryGetValue(e.Path, out var previous) || previous != e).Select(e => (old.ContainsKey(e.Path) ? "Replace: " : "Add: ") + e.Path)
            .Concat(old.Keys.Where(k => !next.ContainsKey(k)).Select(k => "Remove: " + k)).ToList();
        return new SaveRestorePreview(id, Directory.Exists(Target) ? "present:" + Fingerprint(current) : "missing", Fingerprint(snapshot.Entries), Target, changes);
    }, token);
    public Task<SaveRestoreResult> RestoreAsync(SaveRestorePreview preview, CancellationToken token = default) => Task.Run(() =>
    {
        using var gate = Lock(); NoPendingRestore(); var snapshot = Read(preview.SnapshotId, true, token);
        if (!Game.SamePath(preview.Destination, Target) || preview.CurrentFingerprint != Current(token) || preview.SnapshotFingerprint != Fingerprint(snapshot.Entries)) throw new IOException("Saves or snapshot changed. Preview again before restoring.");
        var parent = Path.GetDirectoryName(Target)!;
        if (!Directory.Exists(parent)) throw new DirectoryNotFoundException("The save folder's parent must exist.");
        var id = Guid.NewGuid(); var stage = Path.Combine(parent, ".archive-restore-" + id.ToString("N")); var previous = Path.Combine(parent, ".archive-previous-" + id.ToString("N"));
        CopyTree(Path.Combine(SnapshotPath(snapshot.Id), "files"), stage, snapshot.Entries, token);
        if (Fingerprint(Inventory(stage, token)) != preview.SnapshotFingerprint) throw new InvalidDataException("Staged restore integrity check failed.");
        var safety = Directory.Exists(Target) ? Backup(true, token) : null;
        if (preview.CurrentFingerprint != Current(token)) throw new IOException("Saves changed after preview. Restore cancelled.");
        token.ThrowIfCancellationRequested(); PathSafety.NoLinks(Target); PathSafety.NoLinks(stage); PathSafety.NoLinks(previous);
        WriteNew(Journal, new RestoreJournal(id, Target));
        // After recording intent, finish or recover the swap without cancellation. Original folder is retained.
        if (Directory.Exists(Target)) Directory.Move(Target, previous);
        Directory.Move(stage, Target); File.Delete(Journal);
        return new SaveRestoreResult(safety?.Id, Directory.Exists(previous) ? previous : null);
    }, token);
    public Task<string> RecoverAsync() => Task.Run(() =>
    {
        using var gate = Lock(); if (!File.Exists(Journal)) return "No interrupted restore found.";
        PathSafety.NoLinks(Journal);
        if (new FileInfo(Journal).Length > 4096) throw new InvalidDataException("Invalid recovery record.");
        var record = JsonSerializer.Deserialize<RestoreJournal>(File.ReadAllText(Journal)) ?? throw new InvalidDataException("Invalid recovery record.");
        if (!Game.SamePath(record.Target, Target)) throw new InvalidDataException("Recovery destination differs from the configured folder.");
        var parent = Path.GetDirectoryName(Target)!; var previous = Path.Combine(parent, ".archive-previous-" + record.Id.ToString("N"));
        PathSafety.NoLinks(previous); PathSafety.NoLinks(Target);
        if (!Directory.Exists(Target) && Directory.Exists(previous)) Directory.Move(previous, Target);
        if (!Directory.Exists(Target)) throw new IOException("No previous folder is available. Recovery files were preserved for manual inspection.");
        File.Delete(Journal); return "Recovery completed. Any staged or previous folders were retained beside the save folder.";
    });
}
