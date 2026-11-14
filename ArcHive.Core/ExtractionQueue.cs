using System.Text.Json;
namespace ArcHive.Core;

public enum ExtractionState { Pending, Running, Interrupted, Extracted, Completed, Failed, Cancelled }
public sealed class ExtractionJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public Guid ArchiveId { get; set; }
    public Guid InstallationId { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Archive { get; set; } = "";
    public string Parent { get; set; } = "";
    public long SourceLength { get; set; }
    public DateTime SourceWriteUtc { get; set; }
    public ReleaseInfo Release { get; set; } = new();
    public ExtractionState State { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public string DisplayState => UiText.Extraction(State);
    public List<string> Attempts { get; set; } = [];
    public string? Output { get; set; }
    public string Error { get; set; } = "";
}
public sealed class ExtractionJournal
{
    public int Version { get; set; } = 1;
    public List<ExtractionJob> Jobs { get; set; } = [];
}

// Single owner, flushed atomic journal; no recovery path deletes filesystem contents.
public sealed class ExtractionQueue : IDisposable
{
    private readonly string file;
    private readonly FileStream lease;
    private ExtractionJournal journal;
    private bool running;
    public IReadOnlyList<ExtractionJob> Jobs => journal.Jobs;
    public bool IsRunning => running;
    public void ClearForLibraryReset()
    {
        if (running) throw new InvalidOperationException("Stop extraction before purging the library.");
        if (File.Exists(file)) File.Copy(file, file + ".pre-purge-" + Guid.NewGuid().ToString("N") + ".json");
        var empty = new ExtractionJournal(); Save(empty); journal = empty;
    }
    public ExtractionQueue(string file)
    {
        this.file = Path.GetFullPath(file); Directory.CreateDirectory(Path.GetDirectoryName(this.file)!);
        lease = new FileStream(this.file + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            if (File.Exists(file) && new FileInfo(file).Length > 20 * 1024 * 1024) throw new InvalidDataException("Queue journal exceeds size limit.");
            journal = File.Exists(file) ? JsonSerializer.Deserialize<ExtractionJournal>(File.ReadAllText(file)) ?? throw new InvalidDataException("Invalid queue journal.") : new();
            Validate(journal);
            if (journal.Jobs.Any(j => j.State == ExtractionState.Running))
            {
                foreach (var job in journal.Jobs.Where(j => j.State == ExtractionState.Running)) { job.State = ExtractionState.Interrupted; job.Error = "Extraction was interrupted. Retry to start again. Previous files were retained."; }
                Save(journal);
            }
        }
        catch { lease.Dispose(); throw; }
    }
    private static void Validate(ExtractionJournal value)
    {
        if (value.Version != 1 || value.Jobs.Count > 10000 || value.Jobs.Select(j => j.Id).Distinct().Count() != value.Jobs.Count) throw new InvalidDataException("Unsupported or invalid queue journal.");
        foreach (var job in value.Jobs)
        {
            if (!Enum.IsDefined(job.State) || !Path.IsPathFullyQualified(job.Archive) || !Path.IsPathFullyQualified(job.Parent)) throw new InvalidDataException("Invalid queue job.");
            foreach (var attempt in job.Attempts)
                if (!Game.SamePath(Path.GetDirectoryName(attempt)!, job.Parent) || !Path.GetFileName(attempt).StartsWith("ArcHive-extract-" + job.Id.ToString("N") + "-", StringComparison.Ordinal)) throw new InvalidDataException("Invalid extraction attempt path.");
            if (job.Output is not null && (job.Attempts.Count == 0 || !Game.SamePath(Path.GetDirectoryName(job.Output)!, job.Attempts[^1]))) throw new InvalidDataException("Invalid extraction output path.");
            if (job.State is ExtractionState.Extracted or ExtractionState.Completed && job.Output is null) throw new InvalidDataException("Completed output is missing from journal.");
        }
    }
    private void Save(ExtractionJournal updated)
    {
        Validate(updated);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(updated, new JsonSerializerOptions { WriteIndented = true });
        if (bytes.Length > 20 * 1024 * 1024) throw new InvalidDataException("Queue journal exceeds size limit.");
        using (var output = new FileStream(file + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None)) { output.Write(bytes); output.Flush(true); }
        if (File.Exists(file)) File.Replace(file + ".tmp", file, file + ".bak"); else File.Move(file + ".tmp", file);
        journal = updated;
    }
    private void Change(Guid id, Action<ExtractionJob> change)
    {
        var next = JsonSerializer.Deserialize<ExtractionJournal>(JsonSerializer.Serialize(journal))!;
        change(next.Jobs.Single(j => j.Id == id)); Save(next);
    }
    public void Enqueue(Game game, GameArchive archive, string parent) => EnqueueBatch([(game, archive)], parent);
    public void EnqueueBatch(IEnumerable<(Game Game, GameArchive Archive)> items, string parent)
    {
        if (running) throw new InvalidOperationException("Stop the queue before adding jobs.");
        parent = Path.GetFullPath(parent); PathSafety.NoLinks(parent);
        var next = JsonSerializer.Deserialize<ExtractionJournal>(JsonSerializer.Serialize(journal))!;
        foreach (var (game, archive) in items)
        {
            if (!game.Archives.Any(a => a.Id == archive.Id)) throw new InvalidOperationException("Archive must belong to the game.");
            PathSafety.NoLinks(archive.Path);
            if (next.Jobs.Any(j => j.GameId == game.Id && Game.SamePath(j.Archive, archive.Path) && Game.SamePath(j.Parent, parent) && j.State is ExtractionState.Pending or ExtractionState.Running or ExtractionState.Extracted)) throw new InvalidOperationException("This extraction is already queued.");
            var info = new FileInfo(archive.Path);
            next.Jobs.Add(new() { GameId = game.Id, ArchiveId = archive.Id, Title = game.Title, Archive = Path.GetFullPath(archive.Path), Parent = parent,
                SourceLength = info.Length, SourceWriteUtc = info.LastWriteTimeUtc, Release = archive.Release with { } });
        }
        Save(next);
    }
    public void Retry(Guid id)
    {
        if (running) throw new InvalidOperationException("Stop the queue first.");
        Change(id, job => { if (job.State is not (ExtractionState.Interrupted or ExtractionState.Failed or ExtractionState.Cancelled)) throw new InvalidOperationException("Only interrupted, failed or canceled jobs can be retried."); job.State = ExtractionState.Pending; job.Error = ""; job.Output = null; });
    }
    public void CancelPending(Guid id)
    {
        if (running) throw new InvalidOperationException("Stop the queue first.");
        Change(id, job => { if (job.State != ExtractionState.Pending) throw new InvalidOperationException("Select a pending job."); job.State = ExtractionState.Cancelled; });
    }
    public async Task RunAsync(IArchiveExtractor extractor, Func<ExtractionJob, Task> link, IProgress<string>? progress, CancellationToken token)
    {
        if (running) throw new InvalidOperationException("Queue is already running."); running = true;
        try
        {
            foreach (var id in Jobs.Where(j => j.State is ExtractionState.Pending or ExtractionState.Extracted).Select(j => j.Id).ToArray())
            {
                if (token.IsCancellationRequested) break;
                try
                {
                    var job = Jobs.Single(j => j.Id == id);
                    if (job.State == ExtractionState.Pending)
                    {
                        PathSafety.NoLinks(job.Archive); PathSafety.NoLinks(job.Parent);
                        using var sourceLock = new FileStream(job.Archive, FileMode.Open, FileAccess.Read, FileShare.Read);
                        if (sourceLock.Length != job.SourceLength || File.GetLastWriteTimeUtc(job.Archive) != job.SourceWriteUtc) throw new IOException("The archive has changed since it was queued. Preview it again before extracting.");
                        var preview = await ArchiveInspection.ReadAsync(job.Archive, token);
                        if (!preview.CanExtract) throw new InvalidDataException("Archive preview contains unsupported or unsafe entries.");
                        var attempt = Path.Combine(job.Parent, "ArcHive-extract-" + job.Id.ToString("N") + "-" + Guid.NewGuid().ToString("N"));
                        Change(id, j => { j.State = ExtractionState.Running; j.Attempts.Add(attempt); j.Error = ""; });
                        // Journal is durable before the extractor creates any output.
                        var output = await extractor.ExtractAsync(job.Archive, attempt, progress, token);
                        if (!Game.SamePath(Path.GetDirectoryName(output)!, attempt) || !Directory.Exists(output)) throw new IOException("Extractor returned an unexpected output directory.");
                        PathSafety.NoLinks(output);
                        Change(id, j => { j.Output = output; j.State = ExtractionState.Extracted; });
                    }
                    job = Jobs.Single(j => j.Id == id);
                    PathSafety.NoLinks(job.Output!);
                    if (!Directory.Exists(job.Output)) throw new DirectoryNotFoundException("Extracted output is unavailable. Restore its location before linking.");
                    await link(job);
                    Change(id, j => { j.State = ExtractionState.Completed; j.Error = ""; });
                }
                catch (OperationCanceledException)
                {
                    Change(id, j => { if (j.State != ExtractionState.Extracted) j.State = ExtractionState.Cancelled; j.Error = "Canceled; subsequent jobs remain queued."; }); break;
                }
                catch (Exception ex)
                {
                    Change(id, j => { if (j.State != ExtractionState.Extracted) j.State = ExtractionState.Failed; j.Error = ex.Message; });
                }
            }
        }
        finally { running = false; }
    }
    public static Library Link(Library source, ExtractionJob job)
    {
        if (job.State != ExtractionState.Extracted || job.Output is null || !Directory.Exists(job.Output)) throw new InvalidOperationException("No completed extraction to link.");
        PathSafety.NoLinks(job.Output);
        var copy = JsonSerializer.Deserialize<Library>(JsonSerializer.Serialize(source))!;
        var game = copy.Games.SingleOrDefault(g => g.Id == job.GameId) ?? throw new InvalidOperationException("Game was removed; output retained for manual linking.");
        if (!game.Archives.Any(a => a.Id == job.ArchiveId)) throw new InvalidOperationException("Archive association was removed; output retained for manual linking.");
        var existing = game.Installations.FirstOrDefault(i => i.Id == job.InstallationId);
        if (existing is not null)
        {
            if (!Game.SamePath(existing.Path, job.Output) || existing.SourceArchiveId != job.ArchiveId) throw new InvalidOperationException("Installation identity conflict.");
        }
        else game.Installations.Add(new() { Id = job.InstallationId, Path = job.Output, SourceArchiveId = job.ArchiveId, Release = job.Release with { } });
        return copy;
    }
    public void Dispose() { if (running) throw new InvalidOperationException("Stop the queue before closing."); lease.Dispose(); }
}
