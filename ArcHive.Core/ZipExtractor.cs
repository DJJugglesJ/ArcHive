using System.IO.Compression;

namespace ArcHive.Core;

public static class PathSafety
{
    public static bool IsWithin(string root, string path)
    {
        root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        path = Path.GetFullPath(path);
        return path.Equals(root, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
    public static void NoLinks(string path)
    {
        for (var p = Path.GetFullPath(path); p is not null; p = Path.GetDirectoryName(p))
            if ((Directory.Exists(p) || File.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked files and directories are not supported.");
    }
}

public interface IArchiveExtractor
{
    Task<string> ExtractAsync(string archive, string parent, IProgress<string>? progress, CancellationToken token);
}

public sealed class ZipExtractor(long byteLimit = 20L * 1024 * 1024 * 1024, int entryLimit = 100000) : IArchiveExtractor
{
    public Task<string> ExtractAsync(string archive, string parent, IProgress<string>? progress, CancellationToken token)
        => Task.Run(() => Extract(archive, parent, progress, token), token);

    private string Extract(string archive, string parent, IProgress<string>? progress, CancellationToken token)
    {
        if (!Path.GetExtension(archive).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("This increment extracts ZIP files. RAR and 7z remain indexed; extract them externally and use Link folder.");
        PathSafety.NoLinks(archive);
        PathSafety.NoLinks(parent);
        Directory.CreateDirectory(parent);
        var id = Guid.NewGuid().ToString("N");
        var staging = Path.Combine(parent, ".archive-" + id);
        var destination = Path.Combine(parent, Path.GetFileNameWithoutExtension(archive) + "-" + id[..8]);
        Directory.CreateDirectory(staging);
        try
        {
            using var zip = ZipFile.OpenRead(archive);
            if (zip.Entries.Count > entryLimit) throw new InvalidDataException("Too many archive entries.");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long declared = 0;
            var plan = new List<(ZipArchiveEntry Entry, string Target, bool Directory)>();
            foreach (var entry in zip.Entries)
            {
                token.ThrowIfCancellationRequested();
                var name = entry.FullName.Replace('\\', '/');
                var parts = name.TrimEnd('/').Split('/');
                if (string.IsNullOrEmpty(name) || name.StartsWith('/') || parts.Any(UnsafeSegment))
                    throw new InvalidDataException("Unsafe archive path: " + name);
                var unixType = (entry.ExternalAttributes >> 16) & 0xF000;
                if ((unixType != 0 && unixType != 0x8000 && unixType != 0x4000) ||
                    (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Archive links and special files are not supported.");
                var target = Path.GetFullPath(Path.Combine(staging, Path.Combine(parts)));
                if (!PathSafety.IsWithin(staging, target) || !names.Add(target))
                    throw new InvalidDataException("Duplicate or escaping archive path.");
                if (entry.Length > byteLimit - declared) throw new InvalidDataException("Archive exceeds extraction size limit.");
                declared += entry.Length;
                plan.Add((entry, target, name.EndsWith('/')));
            }
            long written = 0;
            var buffer = new byte[81920];
            foreach (var item in plan)
            {
                token.ThrowIfCancellationRequested();
                PathSafety.NoLinks(staging);
                if (item.Directory) { Directory.CreateDirectory(item.Target); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(item.Target)!);
                PathSafety.NoLinks(item.Target);
                progress?.Report("Extracting " + item.Entry.FullName);
                using var input = item.Entry.Open();
                using var output = new FileStream(item.Target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                int read;
                while ((read = input.Read(buffer)) != 0)
                {
                    token.ThrowIfCancellationRequested();
                    if (read > byteLimit - written) throw new InvalidDataException("Extraction size limit reached.");
                    written += read;
                    output.Write(buffer, 0, read);
                }
            }
            token.ThrowIfCancellationRequested();
            PathSafety.NoLinks(parent);
            Directory.Move(staging, destination);
            return destination;
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
        }
    }

    private static bool UnsafeSegment(string part)
    {
        if (part is "" or "." or ".." || part.EndsWith('.') || part.EndsWith(' ') ||
            part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || part.Contains(':')) return true;
        var stem = part.Split('.')[0].ToUpperInvariant();
        return stem is "CON" or "PRN" or "AUX" or "NUL" ||
            (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && char.IsDigit(stem[3]));
    }
}
