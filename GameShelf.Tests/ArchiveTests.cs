using GameShelf.Core;
using System.Security.Cryptography;

static class ArchiveTests
{
    public static async Task Run(string root, Action<bool, string> check)
    {
        var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        foreach (var path in new[] { "../escape", "file:stream", "CON.txt", "trailing.", "a/../../escape" })
        {
            var malicious = Path.Combine(root, Guid.NewGuid() + ".7z");
            using (var output = File.Create(malicious))
            using (var writer = new SharpCompress.Writers.SevenZip.SevenZipWriter(output, new()))
            using (var input = new MemoryStream(new byte[] { 1, 2, 3 }))
                writer.Write(path, input, null);
            await MustFail(malicious, new MultiArchiveExtractor(), "7z unsafe path " + path);
        }
        foreach (var name in new[] { "Rar.rar", "Rar.solid.rar", "Rar5.rar", "Rar5.solid.rar", "7Zip.LZMA.7z", "7Zip.LZMA2.7z", "7Zip.solid.7z", "7Zip.nonsolid.7z" })
        {
            var source = Path.Combine(fixtures, name);
            var parent = Path.Combine(root, Guid.NewGuid().ToString());
            var folder = await new MultiArchiveExtractor().ExtractAsync(source, parent, null, default);
            var files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);
            check(files.Length == 3, name + " file count");
            foreach (var file in files)
            {
                var original = Path.Combine(fixtures, "Original", Path.GetRelativePath(folder, file));
                check(File.Exists(original) && SHA256.HashData(File.ReadAllBytes(file)).SequenceEqual(SHA256.HashData(File.ReadAllBytes(original))), name + " SHA256 " + Path.GetFileName(file));
            }
            await MustFail(source, new MultiArchiveExtractor(1), "byte limit " + name);
            await MustFail(source, new MultiArchiveExtractor(entryLimit: 1), "entry limit " + name);
            using var cancel = new CancellationTokenSource();
            var destination = Path.Combine(root, Guid.NewGuid().ToString());
            try { await new MultiArchiveExtractor().ExtractAsync(source, destination, new CancelProgress(cancel), cancel.Token); throw new Exception("Cancellation ignored"); }
            catch (OperationCanceledException) { check(!Directory.EnumerateFileSystemEntries(destination).Any(), "cancel cleanup " + name); }
        }
        foreach (var name in new[] { "Rar.encrypted_filesOnly.rar", "7Zip.encryptedFiles.7z" })
            await MustFail(Path.Combine(fixtures, name), new MultiArchiveExtractor(), "encrypted " + name);
        foreach (var ext in new[] { ".rar", ".7z" })
        {
            var bad = Path.Combine(root, "invalid" + ext); File.WriteAllText(bad, "not an archive");
            await MustFail(bad, new MultiArchiveExtractor(), "invalid " + ext);
        }
        async Task MustFail(string source, IArchiveExtractor extractor, string label)
        {
            var destination = Path.Combine(root, Guid.NewGuid().ToString());
            Exception? error = null;
            try { await extractor.ExtractAsync(source, destination, null, default); }
            catch (Exception ex) { error = ex; }
            check(error is not null && !Directory.EnumerateFileSystemEntries(destination).Any(), label);
        }
    }
}

