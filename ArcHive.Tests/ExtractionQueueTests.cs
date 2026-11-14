using System.IO.Compression;
using System.Text.Json;
using ArcHive.Core;

internal static class ExtractionQueueTests
{
    private sealed class Extractor(Func<string, string, IProgress<string>?, CancellationToken, Task<string>> action) : IArchiveExtractor
    { public Task<string> ExtractAsync(string archive, string parent, IProgress<string>? progress, CancellationToken token) => action(archive, parent, progress, token); }
    public static async Task Run(string root, Action<bool, string> check)
    {
        var area = Path.Combine(root, "queue-tests"); Directory.CreateDirectory(area);
        string Zip(string name, params (string Name, string Text)[] entries)
        {
            var path = Path.Combine(area, name);
            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
            foreach (var entry in entries) { using var writer = new StreamWriter(zip.CreateEntry(entry.Name).Open()); writer.Write(entry.Text); }
            return path;
        }
        var path = Zip("game.zip", ("folder/", ""), ("folder/game.exe", "payload"), ("readme.txt", "notes"));
        var preview = await ArchiveInspection.ReadAsync(path);
        check(preview.CanExtract && preview.Entries.Count == 3 && preview.ExpandedBytes == 12 && preview.ArchiveBytes == new FileInfo(path).Length, "ZIP preview reports paths, directories and compressed/expanded byte totals");
        check(!Directory.Exists(Path.Combine(area, "folder")), "Preview writes no extracted files");
        foreach (var fixture in new[] { "Rar.rar", "Rar5.solid.rar", "7Zip.solid.7z", "7Zip.nonsolid.7z" })
            check((await ArchiveInspection.ReadAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture))).Entries.Count >= 3, "Preview reads " + fixture);
        var bad = Zip("unsafe.zip", ("../escape", "x"), ("CON.txt", "x"), ("a", "x"), ("a/child", "x"), ("A", "x"));
        var unsafePreview = await ArchiveInspection.ReadAsync(bad);
        check(!unsafePreview.CanExtract && unsafePreview.Entries.Count(e => e.Issue.Length > 0) == 4, "Preview flags traversal, reserved names, file-parent conflicts and case-insensitive duplicates");
        check(!(await ArchiveInspection.ReadAsync(path, byteLimit: 1)).CanExtract, "Preview flags expanded-size policy violations");
        try { await ArchiveInspection.ReadAsync(path, entryLimit: 1); throw new Exception("Entry limit ignored"); }
        catch (InvalidDataException) { check(true, "Preview enforces entry count limit"); }
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            try { await ArchiveInspection.ReadAsync(path, cancelled.Token); throw new Exception("Cancellation ignored"); }
            catch (OperationCanceledException) { check(true, "Archive preview honors cancellation"); }
        }
        var game = new Game { Title = "Queued game", Archives = [new() { Path = path, Release = new() { Version = "1.2" } }] };
        var library = new Library { Games = [game] }; var journalPath = Path.Combine(area, "queue.json");
        var destination = Path.Combine(area, "output");
        var db = new SqliteLibraryStore(Path.Combine(area, "library.db")); db.Load(); db.Save(library);
        using (var queue = new ExtractionQueue(journalPath))
        {
            queue.Enqueue(game, game.Archives[0], destination);
            check(queue.Jobs.Single().State == ExtractionState.Pending && !Directory.Exists(destination), "Enqueue persists pending work without creating extraction output");
            try { using var other = new ExtractionQueue(journalPath); throw new Exception("Concurrent queue opened"); }
            catch (IOException) { check(true, "Queue lease prevents simultaneous journal writers"); }
            try { queue.Enqueue(game, game.Archives[0], destination); throw new Exception("Duplicate queued"); }
            catch (InvalidOperationException) { check(queue.Jobs.Count == 1, "Duplicate active extraction is rejected"); }
        }
        string? finished = null; Guid installationId = default;
        using (var queue = new ExtractionQueue(journalPath))
        {
            check(queue.Jobs.Count == 1 && queue.Jobs[0].SourceLength == new FileInfo(path).Length, "Pending extraction and source fingerprint survive restart");
            await queue.RunAsync(new MultiArchiveExtractor(), job =>
            {
                // Simulate a crash after library commit but before queue completion acknowledgment.
                library = ExtractionQueue.Link(library, job); db.Save(library);
                File.Copy(journalPath, journalPath + ".after-library", true);
                return Task.CompletedTask;
            }, null, default);
            var job = queue.Jobs.Single(); finished = job.Output; installationId = job.InstallationId;
            check(job.State == ExtractionState.Completed && File.ReadAllText(Path.Combine(finished!, "folder/game.exe")) == "payload", "Queue extracts and publishes a complete installation");
            check(library.Games[0].Installations.Single().Release.Version == "1.2" && library.Games[0].Installations[0].SourceArchiveId == game.Archives[0].Id, "Queue linking preserves archive lineage and release metadata");
            var calls = 0;
            await queue.RunAsync(new Extractor((_, _, _, _) => { calls++; throw new Exception(); }), _ => Task.CompletedTask, null, default);
            check(calls == 0, "Completed jobs are never re-extracted");
        }
        File.Copy(journalPath + ".after-library", journalPath, true);
        using (var queue = new ExtractionQueue(journalPath))
        {
            check(queue.Jobs.Single().State == ExtractionState.Extracted, "Recovery retains completed output awaiting acknowledgment");
            await queue.RunAsync(new Extractor((_, _, _, _) => throw new Exception("Must not extract")), job => { library = ExtractionQueue.Link(db.Load(), job); db.Save(library); return Task.CompletedTask; }, null, default);
            check(queue.Jobs[0].State == ExtractionState.Completed && db.Load().Games[0].Installations.Count == 1 && db.Load().Games[0].Installations[0].Id == installationId, "Replay after library-commit crash links idempotently with no duplicate installation");
        }
        var crashPath = Path.Combine(area, "crash.json"); string? orphan = null;
        using (var queue = new ExtractionQueue(crashPath))
        {
            queue.Enqueue(game, game.Archives[0], destination);
            await queue.RunAsync(new Extractor(async (a, p, progress, token) =>
            {
                orphan = await new ZipExtractor().ExtractAsync(a, p, progress, token);
                File.Copy(crashPath, crashPath + ".interrupted", true); // Running receipt, but directory already published.
                return orphan;
            }), _ => Task.CompletedTask, null, default);
        }
        File.Copy(crashPath + ".interrupted", crashPath, true);
        using (var queue = new ExtractionQueue(crashPath))
        {
            var job = queue.Jobs[0];
            check(job.State == ExtractionState.Interrupted && Directory.Exists(orphan), "Crash before output receipt marks interruption and retains uncertain output");
            var calls = 0; await queue.RunAsync(new Extractor((_, _, _, _) => { calls++; throw new Exception(); }), _ => Task.CompletedTask, null, default);
            check(calls == 0, "Interrupted jobs never automatically resume");
            queue.Retry(job.Id);
            await queue.RunAsync(new ZipExtractor(), _ => Task.CompletedTask, null, default);
            check(queue.Jobs[0].State == ExtractionState.Completed && queue.Jobs[0].Attempts.Count == 2 && queue.Jobs[0].Output != orphan && File.Exists(Path.Combine(orphan!, "folder/game.exe")), "Explicit retry uses a fresh attempt and retains previous output");
        }
        using (var queue = new ExtractionQueue(Path.Combine(area, "link-fail.json")))
        {
            queue.Enqueue(game, game.Archives[0], destination);
            await queue.RunAsync(new ZipExtractor(), _ => throw new IOException("Database unavailable"), null, default);
            check(queue.Jobs[0].State == ExtractionState.Extracted && Directory.Exists(queue.Jobs[0].Output) && queue.Jobs[0].Error.Contains("Database unavailable"), "Database failure retains extracted output for retry");
            await queue.RunAsync(new Extractor((_, _, _, _) => throw new Exception("Must not re-extract")), _ => Task.CompletedTask, null, default);
            check(queue.Jobs[0].State == ExtractionState.Completed, "Link retry does not re-extract files");
        }
        using (var queue = new ExtractionQueue(Path.Combine(area, "cancel.json")))
        {
            queue.Enqueue(game, game.Archives[0], Path.Combine(area, "cancel1")); queue.Enqueue(game, game.Archives[0], Path.Combine(area, "cancel2"));
            using var cancel = new CancellationTokenSource();
            await queue.RunAsync(new Extractor((_, _, _, _) => { cancel.Cancel(); throw new OperationCanceledException(); }), _ => Task.CompletedTask, null, cancel.Token);
            check(queue.Jobs[0].State == ExtractionState.Cancelled && queue.Jobs[1].State == ExtractionState.Pending, "Stopping current extraction leaves subsequent jobs pending");
            queue.CancelPending(queue.Jobs[1].Id); queue.Retry(queue.Jobs[0].Id);
            check(queue.Jobs[0].State == ExtractionState.Pending && queue.Jobs[1].State == ExtractionState.Cancelled, "Cancelled jobs can be explicitly retried and pending jobs cancelled");
        }
        var changedPath = Zip("changed.zip", ("game.exe", "original")); var changedGame = new Game { Archives = [new() { Path = changedPath }] };
        using (var queue = new ExtractionQueue(Path.Combine(area, "changed.json")))
        {
            queue.Enqueue(changedGame, changedGame.Archives[0], destination); File.AppendAllText(changedPath, "modified");
            queue.Enqueue(game, game.Archives[0], destination);
            await queue.RunAsync(new ZipExtractor(), _ => Task.CompletedTask, null, default);
            check(queue.Jobs[0].State == ExtractionState.Failed && queue.Jobs[0].Attempts.Count == 0 && queue.Jobs[1].State == ExtractionState.Completed, "Changed archive fails before extraction while next queued job continues");
        }
        using (var queue = new ExtractionQueue(Path.Combine(area, "unsafe.json")))
        {
            var unsafeGame = new Game { Archives = [new() { Path = bad }] }; queue.Enqueue(unsafeGame, unsafeGame.Archives[0], destination);
            await queue.RunAsync(new ZipExtractor(), _ => Task.CompletedTask, null, default);
            check(queue.Jobs[0].State == ExtractionState.Failed && queue.Jobs[0].Attempts.Count == 0, "Unsafe preview prevents queue extraction");
        }
        using (var queue = new ExtractionQueue(Path.Combine(area, "pre-cancel.json")))
        {
            queue.Enqueue(game, game.Archives[0], destination);
            using var stop = new CancellationTokenSource(); stop.Cancel();
            await queue.RunAsync(new Extractor((_, _, _, _) => throw new Exception("Must not start")), _ => Task.CompletedTask, null, stop.Token);
            check(queue.Jobs[0].State == ExtractionState.Pending && queue.Jobs[0].Attempts.Count == 0, "Pre-cancelled queue does not start pending work");
        }
        var writeFailure = Path.Combine(area, "write-failure.json");
        using (var queue = new ExtractionQueue(writeFailure))
        {
            Directory.CreateDirectory(writeFailure + ".tmp");
            try { queue.Enqueue(game, game.Archives[0], destination); throw new Exception("Journal write unexpectedly succeeded"); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { check(queue.Jobs.Count == 0 && !File.Exists(writeFailure), "Journal write failure does not publish an in-memory queued job"); }
        }
        using (var queue = new ExtractionQueue(Path.Combine(area, "source-lock.json")))
        {
            queue.Enqueue(game, game.Archives[0], destination); var blocked = false;
            await queue.RunAsync(new Extractor(async (a, p, progress, token) =>
            {
                try { using var writer = new FileStream(a, FileMode.Open, FileAccess.Write, FileShare.ReadWrite); }
                catch (IOException) { blocked = true; }
                return await new ZipExtractor().ExtractAsync(a, p, progress, token);
            }), _ => Task.CompletedTask, null, default);
            check(blocked && queue.Jobs[0].State == ExtractionState.Completed, "Archive remains protected from writes across preflight and extraction");
            try { queue.Retry(queue.Jobs[0].Id); throw new Exception("Completed job retried"); }
            catch (InvalidOperationException) { check(true, "Completed jobs cannot be reset through Retry"); }
        }
        var partialJournal = Path.Combine(area, "partial.json"); string? partial = null;
        using (var queue = new ExtractionQueue(partialJournal))
        {
            queue.Enqueue(game, game.Archives[0], destination);
            await queue.RunAsync(new Extractor((a, p, progress, token) =>
            {
                partial = Path.Combine(p, ".archive-partial"); Directory.CreateDirectory(partial); File.WriteAllText(Path.Combine(partial, "unfinished"), "partial bytes");
                File.Copy(partialJournal, partialJournal + ".crash", true);
                throw new IOException("Simulated interrupted decoder");
            }), _ => Task.CompletedTask, null, default);
        }
        File.Copy(partialJournal + ".crash", partialJournal, true);
        using (var queue = new ExtractionQueue(partialJournal))
        {
            check(queue.Jobs[0].State == ExtractionState.Interrupted && File.ReadAllText(Path.Combine(partial!, "unfinished")) == "partial bytes", "Recovery retains partial staging without treating it as completed output");
            queue.Retry(queue.Jobs[0].Id);
            await queue.RunAsync(new ZipExtractor(), _ => Task.CompletedTask, null, default);
            check(queue.Jobs[0].State == ExtractionState.Completed && File.Exists(Path.Combine(partial!, "unfinished")), "Retry extracts fresh while preserving partial staging for manual review");
        }
        var corrupt = Path.Combine(area, "corrupt.json"); File.WriteAllText(corrupt, "{broken");
        try { using var queue = new ExtractionQueue(corrupt); throw new Exception("Corrupt queue accepted"); }
        catch (JsonException) { check(File.ReadAllText(corrupt) == "{broken", "Corrupt journal is preserved without silent reset"); }
        File.WriteAllText(corrupt, "{\"Version\":99,\"Jobs\":[]}");
        try { using var queue = new ExtractionQueue(corrupt); throw new Exception("Future journal accepted"); }
        catch (InvalidDataException) { check(true, "Unknown journal version rejected"); }
        var invalid = JsonSerializer.Deserialize<ExtractionJournal>(File.ReadAllText(journalPath))!;
        invalid.Jobs[0].Output = Path.Combine(area, "unrelated"); File.WriteAllText(corrupt, JsonSerializer.Serialize(invalid));
        try { using var queue = new ExtractionQueue(corrupt); throw new Exception("Invalid output accepted"); }
        catch (InvalidDataException) { check(Directory.Exists(finished), "Recovery rejects output outside recorded attempt and leaves existing files untouched"); }
        var unlinked = JsonSerializer.Deserialize<ExtractionJournal>(File.ReadAllText(journalPath + ".after-library"))!.Jobs[0];
        try { ExtractionQueue.Link(new(), unlinked); throw new Exception("Missing game linked"); }
        catch (InvalidOperationException) { check(Directory.Exists(unlinked.Output), "Removed game cannot be silently recreated by recovery"); }
        check(File.Exists(path) && File.ReadAllText(Path.Combine(finished!, "readme.txt")) == "notes", "Queue and recovery preserve archive and published file contents");
    }
}
