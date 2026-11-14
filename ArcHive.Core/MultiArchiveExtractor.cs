using SharpCompress.Archives;

namespace ArcHive.Core;

public sealed class MultiArchiveExtractor(long byteLimit = 20L * 1024 * 1024 * 1024, int entryLimit = 100000) : IArchiveExtractor
{
    public Task<string> ExtractAsync(string archive, string parent, IProgress<string>? progress, CancellationToken token)
        => Task.Run(() => Extract(archive, parent, progress, token), token);

    private string Extract(string archive, string parent, IProgress<string>? progress, CancellationToken token)
    {
        if (Path.GetExtension(archive).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            return new ZipExtractor(byteLimit, entryLimit).ExtractAsync(archive, parent, progress, token).GetAwaiter().GetResult();
        if (Path.GetExtension(archive).ToLowerInvariant() is not (".zip" or ".rar" or ".7z"))
            throw new NotSupportedException("Only ZIP, RAR and 7z archives are supported.");
        PathSafety.NoLinks(archive);
        PathSafety.NoLinks(parent);
        Directory.CreateDirectory(parent);
        var id = Guid.NewGuid().ToString("N");
        var staging = Path.Combine(parent, ".archive-" + id);
        var destination = Path.Combine(parent, Path.GetFileNameWithoutExtension(archive) + "-" + id[..8]);
        Directory.CreateDirectory(staging);
        try
        {
            using var source = File.OpenRead(archive);
            using var zip = ArchiveFactory.OpenArchive(source);
            if (zip.Entries.Take(entryLimit + 1).Count() > entryLimit) throw new InvalidDataException("Too many archive entries.");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long declared = 0;
            var plan = new List<(IArchiveEntry Entry, string Target, bool Directory)>();
            foreach (var entry in zip.Entries)
            {
                token.ThrowIfCancellationRequested();
                var name = (entry.Key ?? "").Replace('\\', '/');
                var parts = name.TrimEnd('/').Split('/');
                if (string.IsNullOrEmpty(name) || name.StartsWith('/') || parts.Any(UnsafeSegment))
                    throw new InvalidDataException("Unsafe archive path: " + name);
                if (entry.IsEncrypted || !entry.IsComplete || entry.IsSplitAfter || !string.IsNullOrEmpty(entry.LinkTarget))
                    throw new InvalidDataException("Encrypted, incomplete and linked entries are not supported.");
                var attributes = entry.Attrib ?? 0;
                var mode = (attributes >> 16) & 0xF000;
                var lowMode = attributes & 0xF000;
                if ((attributes & 0x400) != 0 ||
                    (mode != 0 && mode != 0x8000 && mode != 0x4000) ||
                    (lowMode != 0 && lowMode != 0x8000 && lowMode != 0x4000))
                    throw new InvalidDataException("Archive special files are not supported.");
                if (entry.Size < 0) throw new InvalidDataException("Invalid entry size.");
                var target = Path.GetFullPath(Path.Combine(staging, Path.Combine(parts)));
                if (!PathSafety.IsWithin(staging, target) || !names.Add(target))
                    throw new InvalidDataException("Duplicate or escaping archive path.");
                if (entry.Size > byteLimit - declared) throw new InvalidDataException("Archive exceeds extraction size limit.");
                declared += entry.Size;
                plan.Add((entry, target, entry.IsDirectory));
            }
            long written = 0;
            var buffer = new byte[81920];
            using var reader = zip.IsSolid || zip.Type == SharpCompress.Common.ArchiveType.SevenZip ? zip.ExtractAllEntries() : null;
            IEnumerable<(IArchiveEntry Entry, string Target, bool Directory)> Ordered()
            {
                if (reader is null) { foreach (var item in plan) yield return item; yield break; }
                var remaining = plan.ToDictionary(p => p.Entry.Key!, StringComparer.Ordinal);
                while (reader.MoveToNextEntry())
                {
                    if (!remaining.Remove(reader.Entry.Key!, out var item)) throw new InvalidDataException("Unexpected archive entry.");
                    yield return item;
                }
                if (remaining.Values.Any(p => !p.Directory)) throw new InvalidDataException("Missing archive entries.");
                foreach (var item in remaining.Values) Directory.CreateDirectory(item.Target);
            }
            foreach (var item in Ordered())
            {
                token.ThrowIfCancellationRequested();

                PathSafety.NoLinks(staging);
                if (item.Directory) { Directory.CreateDirectory(item.Target); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(item.Target)!);
                PathSafety.NoLinks(item.Target);
                progress?.Report("Extracting " + item.Entry.Key);
                using var input = reader is null ? item.Entry.OpenEntryStream() : reader.OpenEntryStream();
                using var output = new FileStream(item.Target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                int read;
                long entryWritten = 0;
                while ((read = input.Read(buffer)) != 0)
                {
                    token.ThrowIfCancellationRequested();
                    if (read > byteLimit - written) throw new InvalidDataException("Extraction size limit reached.");
                    written += read;
                    entryWritten += read;
                    if (entryWritten > item.Entry.Size) throw new InvalidDataException("Entry exceeds its declared size.");
                    output.Write(buffer, 0, read);
                }
                if (entryWritten != item.Entry.Size) throw new InvalidDataException("Incomplete archive entry.");
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



