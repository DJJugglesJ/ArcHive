using System.Security.Cryptography;
using System.Text.Json;
namespace ArcHive.Core;

public sealed record FileMove(string Source, string Destination, bool Directory, string Fingerprint);
public sealed record FileMovePlan(string Before, string After, Library Result, FileMove[] Moves);
public sealed record FileMoveJournal(int Version, FileMovePlan Plan);
public static class LibraryFileMoves
{
    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
    private static string Stamp(string path, bool directory)
    {
        PathSafety.NoLinks(path);
        var files = new List<string>(); var entries = new List<string>(); var pending = new Stack<string>();
        if (directory) pending.Push(path); else files.Add(path);
        while (pending.TryPop(out var folder))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(folder))
            {
                if (entries.Count + files.Count > 50000) throw new IOException("Move verification exceeds the 50,000-entry limit.");
                PathSafety.NoLinks(entry);
                if (Directory.Exists(entry)) { entries.Add("D:" + Path.GetRelativePath(path, entry)); pending.Push(entry); }
                else files.Add(entry);
            }
        }
        foreach (var file in files.Order(StringComparer.OrdinalIgnoreCase))
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            entries.Add((directory ? Path.GetRelativePath(path, file) : Path.GetFileName(file)) + ":" + stream.Length + ":" + Convert.ToHexString(SHA256.HashData(stream)));
        }
        // The filename itself changes for a single-file rename.
        if (!directory && entries.Count == 1) entries[0] = entries[0][(entries[0].IndexOf(':') + 1)..];
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", entries.Order(StringComparer.Ordinal)))));
    }
    public static FileMovePlan Preview(Library source, IEnumerable<Guid> selection, string? destinationFolder, bool rename, string namingTemplate = "{title}")
    {
        var ids = selection.ToHashSet(); if (ids.Count == 0) throw new ArgumentException("Select games first.");
        if (destinationFolder is not null) { destinationFolder = Path.GetFullPath(destinationFolder); PathSafety.NoLinks(destinationFolder); if (!Directory.Exists(destinationFolder)) throw new DirectoryNotFoundException("Choose an existing destination folder."); }
        var moves = new List<FileMove>();
        foreach (var game in source.Games.Where(g => ids.Contains(g.Id)))
        {
            if (game.Sessions.Any(s => s.Active)) throw new InvalidOperationException("Close tracked games before moving their files.");
            var title = game.Naming.CanonicalTitle.Length > 0 ? game.Naming.CanonicalTitle : game.Title;
            if (rename && (title.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || title.TrimEnd(' ', '.') != title || title.Length > 120 || string.IsNullOrWhiteSpace(title) || title is "." or ".." || System.Text.RegularExpressions.Regex.IsMatch(title, @"^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(?:\.|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase))) throw new IOException("The title is not a safe filename: " + title);
            foreach (var location in game.Installations.Select(i => (i.Path, Directory: true, i.Release)).Concat(game.Archives.Select(a => (a.Path, Directory: false, a.Release))))
            {
                var path = Path.GetFullPath(location.Path); var parent = destinationFolder ?? Path.GetDirectoryName(path)!;
                var name = rename ? NamingTemplates.Render(namingTemplate, title, path, location.Release) + (location.Directory ? "" : Path.GetExtension(path)) : Path.GetFileName(path);
                var target = Path.Combine(parent, name); if (Game.SamePath(path, target)) continue;
                if (!string.Equals(Path.GetPathRoot(path), Path.GetPathRoot(target), StringComparison.OrdinalIgnoreCase)) throw new IOException("This operation supports moves on the same drive only.");
                if (source.Roots.Any(r => Game.SamePath(r, path))) throw new IOException("A registered library root cannot be moved. Select a game subfolder.");
                if (source.Games.SelectMany(g => g.SaveLocations).Any(s => PathSafety.IsWithin(path, s.Folder))) throw new IOException("This folder contains a configured save location. Move it separately after reviewing its backup configuration.");
                if (Exists(target)) throw new IOException("Destination already exists: " + target);
                PathSafety.NoLinks(parent); moves.Add(new(path, target, location.Directory, Stamp(path, location.Directory)));
            }
        }
        if (moves.Count == 0) throw new InvalidOperationException("No file changes are needed.");
        if (moves.Select(m => m.Source).Distinct(StringComparer.OrdinalIgnoreCase).Count() != moves.Count || moves.Select(m => m.Destination).Distinct(StringComparer.OrdinalIgnoreCase).Count() != moves.Count) throw new IOException("Selected games share paths or destinations. Review them individually.");
        foreach (var m in moves) foreach (var n in moves)
            if (PathSafety.IsWithin(m.Source, n.Destination) || (!ReferenceEquals(m, n) && PathSafety.IsWithin(m.Source, n.Source))) throw new IOException("Nested or overlapping moves are not supported.");
        var copy = JsonSerializer.Deserialize<Library>(JsonSerializer.Serialize(source))!;
        string? Map(string? path)
        {
            if (string.IsNullOrEmpty(path) || !Path.IsPathFullyQualified(path)) return path;
            foreach (var m in moves) if (Game.SamePath(path, m.Source)) return m.Destination;
                else if (m.Directory && PathSafety.IsWithin(m.Source, path)) return Path.Combine(m.Destination, Path.GetRelativePath(m.Source, path));
            return path;
        }
        foreach (var game in copy.Games)
        {
            foreach (var a in game.Archives) a.Path = Map(a.Path)!;
            foreach (var i in game.Installations) { i.Path = Map(i.Path)!; i.ExecutablePath = Map(i.ExecutablePath); foreach (var p in i.Profiles) { p.ExecutablePath = Map(p.ExecutablePath)!; p.WorkingDirectory = Map(p.WorkingDirectory)!; } }
            game.ScreenshotPath = Map(game.ScreenshotPath);
        }
        return new(DuplicateMerge.Fingerprint(source), DuplicateMerge.Fingerprint(copy), copy, moves.ToArray());
    }
    private static void Write(string path, FileMoveJournal journal)
    { if (JsonSerializer.SerializeToUtf8Bytes(journal).Length > 64 * 1024 * 1024) throw new IOException("File operation journal exceeds its size limit."); Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!); using (var f = new FileStream(path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None)) { JsonSerializer.Serialize(f, journal); f.Flush(true); } File.Move(path + ".tmp", path, true); }
    private static void Move(FileMove m, bool reverse)
    { var from = reverse ? m.Destination : m.Source; var to = reverse ? m.Source : m.Destination; PathSafety.NoLinks(from); PathSafety.NoLinks(Path.GetDirectoryName(to)!); if (Exists(to)) throw new IOException("Recovery destination already exists: " + to); if (m.Directory) Directory.Move(from, to); else File.Move(from, to); }
    public static Library Apply(Library current, FileMovePlan plan, string journalPath, Action<Library> save)
    {
        if (File.Exists(journalPath)) throw new IOException("Recover the pending file operation first.");
        if (DuplicateMerge.Fingerprint(current) != plan.Before) throw new IOException("Library changed. Preview the file changes again.");
        foreach (var m in plan.Moves) if (Exists(m.Destination) || Stamp(m.Source, m.Directory) != m.Fingerprint) throw new IOException("Files changed. Preview again.");
        Write(journalPath, new(1, plan));
        // Keep the journal on any failure. Recovery uses the persisted library to decide which side is authoritative.
        foreach (var m in plan.Moves) { if (Stamp(m.Source, m.Directory) != m.Fingerprint) throw new IOException("Source changed during the move."); Move(m, false); }
        foreach (var m in plan.Moves) if (Stamp(m.Destination, m.Directory) != m.Fingerprint) throw new IOException("Moved files changed before the library could be saved.");
        save(plan.Result); File.Move(journalPath, journalPath + ".completed-" + Guid.NewGuid().ToString("N")); return plan.Result;
    }
    public static string Recover(Library current, string journalPath)
    {
        if (!File.Exists(journalPath)) return "No pending file operations.";
        if (new FileInfo(journalPath).Length > 64 * 1024 * 1024) throw new IOException("Recovery journal exceeds its size limit.");
        PathSafety.NoLinks(journalPath);
        var journal = JsonSerializer.Deserialize<FileMoveJournal>(File.ReadAllText(journalPath)) ?? throw new IOException("Invalid recovery journal.");
        if (journal.Version != 1 || journal.Plan.Moves.Length == 0 || journal.Plan.Moves.Any(m => !Path.IsPathFullyQualified(m.Source) || !Path.IsPathFullyQualified(m.Destination))) throw new IOException("Unsupported recovery journal.");
        var fingerprint = DuplicateMerge.Fingerprint(current);
        if (fingerprint != journal.Plan.Before && fingerprint != journal.Plan.After) throw new IOException("Library changed after the interrupted move. Keep the journal and recover paths manually.");
        var committed = fingerprint == journal.Plan.After;
        foreach (var m in journal.Plan.Moves.Reverse())
        {
            var source = Exists(m.Source); var destination = Exists(m.Destination);
            if (source == destination) throw new IOException("Ambiguous recovery paths; no files will be overwritten.");
            var existing = source ? m.Source : m.Destination;
            if (Stamp(existing, m.Directory) != m.Fingerprint) throw new IOException("Files changed after interruption; retain both the journal and files for manual review.");
            if (committed && source) throw new IOException("Committed move is missing its destination; manual recovery is required.");
            if (!committed && destination) Move(m, true);
        }
        File.Move(journalPath, journalPath + ".recovered-" + Guid.NewGuid().ToString("N"));
        return committed ? "Verified committed file changes." : "Restored original paths after the interrupted operation.";
    }
}
