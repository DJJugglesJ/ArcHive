using System.IO.Compression;
#if MULTI_ARCHIVE
using SharpCompress.Archives;
#endif
namespace ArcHive.Core;

public sealed record ArchiveContent(string Path, long Bytes, bool Directory, string Issue);
public sealed record ArchivePreview(long ArchiveBytes, long ExpandedBytes, IReadOnlyList<ArchiveContent> Entries)
{
    public bool CanExtract => Entries.All(e => e.Issue.Length == 0);
}
public static class ArchiveInspection
{
    public static Task<ArchivePreview> ReadAsync(string path, CancellationToken token = default, int entryLimit = 100000, long byteLimit = 20L * 1024 * 1024 * 1024)
        => Task.Run(() => Read(path, token, entryLimit, byteLimit), token);
    private static ArchivePreview Read(string path, CancellationToken token, int entryLimit, long byteLimit)
    {
        PathSafety.NoLinks(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var archiveBytes = stream.Length;
        var rows = new List<ArchiveContent>(); var names = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase); long total = 0;
        void Add(string name, long size, bool directory, bool unsupported)
        {
            token.ThrowIfCancellationRequested();
            if (rows.Count >= entryLimit) throw new InvalidDataException("Too many archive entries to preview.");
            name = name.Replace('\\', '/'); var parts = name.TrimEnd('/').Split('/');
            var issue = "";
            if (string.IsNullOrEmpty(name) || name.StartsWith('/') || parts.Any(p => p is "" or "." or ".." || p.EndsWith('.') || p.EndsWith(' ') || p.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || p.Contains(':') || Reserved(p))) issue = "Unsafe path";
            if (!names.TryAdd(name.TrimEnd('/'), directory)) issue = "Duplicate path";
            if (unsupported) issue = "Encrypted, incomplete, linked or special entry";
            if (size < 0 || size > byteLimit - total) issue = "Extraction size limit exceeded";
            if (size >= 0) total = size > long.MaxValue - total ? long.MaxValue : total + size;
            rows.Add(new(name, size, directory, issue));
        }
        if (Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            foreach (var entry in archive.Entries)
            {
                var mode = (entry.ExternalAttributes >> 16) & 0xF000;
                Add(entry.FullName, entry.Length, (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\')),
                    (mode != 0 && mode != 0x8000 && mode != 0x4000) || (entry.ExternalAttributes & 0x400) != 0);
            }
        }
#if MULTI_ARCHIVE
        else if (Path.GetExtension(path).ToLowerInvariant() is ".rar" or ".7z")
        {
            using var archive = ArchiveFactory.OpenArchive(stream);
            foreach (var entry in archive.Entries)
            {
                var attributes = entry.Attrib ?? 0; var mode = (attributes >> 16) & 0xF000; var low = attributes & 0xF000;
                Add(entry.Key ?? "", entry.Size, entry.IsDirectory, entry.IsEncrypted || !entry.IsComplete || entry.IsSplitAfter || !string.IsNullOrEmpty(entry.LinkTarget) ||
                    (attributes & 0x400) != 0 || (mode != 0 && mode != 0x8000 && mode != 0x4000) || (low != 0 && low != 0x8000 && low != 0x4000));
            }
        }
#endif
        else throw new NotSupportedException("Unsupported archive format.");
        for (var i = 0; i < rows.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            var parent = rows[i].Path.TrimEnd('/');
            while (parent.LastIndexOf('/') is var slash && slash >= 0)
            {
                parent = parent[..slash];
                if (names.TryGetValue(parent, out var isDirectory) && !isDirectory) rows[i] = rows[i] with { Issue = "Parent path is a file" };
            }
        }
        return new(archiveBytes, total, rows);
    }
    private static bool Reserved(string part)
    {
        var stem = part.Split('.')[0].ToUpperInvariant();
        return stem is "CON" or "PRN" or "AUX" or "NUL" || (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && char.IsDigit(stem[3]));
    }
}
