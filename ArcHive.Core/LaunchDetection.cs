using System.Text.Json;
namespace ArcHive.Core;

public sealed record LaunchCandidate(string Path, string Engine, int Rank, string[] Evidence)
{
    public string EvidenceText => string.Join("; ", Evidence);
}
public sealed record EngineSuggestion(string Name, string[] Evidence)
{
    public override string ToString() => Name + " — " + string.Join("; ", Evidence);
}
public sealed record DetectionReport(Guid InstallationId, string Root, IReadOnlyList<LaunchCandidate> Candidates, IReadOnlyList<EngineSuggestion> Engines, IReadOnlyList<string> Notes);
public sealed record DetectionDecision(DateTimeOffset At, string Installation, string Executable, string Engine, string[] Evidence);

public static class LaunchDetection
{
    private static readonly string[] ToolFolders = ["redist", "_commonredist", "prerequisites", "tools", "installer", "installers", "__macosx"];
    public static bool IsHelper(string path)
    {
        var name = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        return name.StartsWith("unins") || name.Contains("crash") || name.StartsWith("vcredist") || name.StartsWith("vc_redist") ||
            name is "setup" or "install" or "installer" or "dxsetup" or "updater" or "update" or "python" or "pythonw" or "ffmpeg" or "reporter";
    }
    public static Task<DetectionReport> ScanAsync(GameInstallation installation, CancellationToken token = default, int entryLimit = 20000, int depthLimit = 12)
        => Task.Run(() => Scan(installation, token, entryLimit, depthLimit), token);
    private static DetectionReport Scan(GameInstallation installation, CancellationToken token, int limit, int depthLimit)
    {
        var root = Path.GetFullPath(installation.Path); PathSafety.NoLinks(root);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException("Installation folder is unavailable.");
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var notes = new List<string>(); var pending = new Stack<(string Path, int Depth)>(); pending.Push((root, 0)); var visited = 0; var limited = false;
        while (pending.TryPop(out var item) && !limited)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                PathSafety.NoLinks(item.Path);
                foreach (var path in Directory.EnumerateFileSystemEntries(item.Path))
                {
                    token.ThrowIfCancellationRequested();
                    if (++visited > limit) { notes.Add("Entry limit reached; results are incomplete."); limited = true; break; }
                    var attributes = File.GetAttributes(path);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) { notes.Add("Skipped linked path: " + Path.GetRelativePath(root, path)); continue; }
                    if ((attributes & FileAttributes.Directory) == 0) { files.Add(path); continue; }
                    directories.Add(path);
                    if (ToolFolders.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)) { notes.Add("Skipped support folder: " + Path.GetRelativePath(root, path)); continue; }
                    if (item.Depth >= depthLimit) { notes.Add("Depth limit: " + Path.GetRelativePath(root, path)); continue; }
                    pending.Push((path, item.Depth + 1));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { notes.Add(item.Path + ": " + ex.Message); }
        }
        var candidates = new List<LaunchCandidate>(); var engines = new Dictionary<string, HashSet<string>>();
        foreach (var exe in files.Where(f => f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.OrdinalIgnoreCase))
        {
            token.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(root, exe);
            if (IsHelper(exe)) { notes.Add("Excluded helper executable: " + relative); continue; }
            var folder = Path.GetDirectoryName(exe)!; var stem = Path.GetFileNameWithoutExtension(exe);
            bool FileAt(string suffix) => files.Contains(Path.Combine(folder, suffix.Replace('/', Path.DirectorySeparatorChar)));
            bool DirAt(string suffix) => directories.Contains(Path.Combine(folder, suffix));
            var evidence = new List<string> { "Executable: " + relative }; var engine = "Unknown"; var rank = Game.SamePath(root, folder) ? 20 : 10;
            void Hint(string name, params string[] markers)
            {
                engine = name; rank += 50; evidence.AddRange(markers.Select(m => "Observed: " + Path.GetRelativePath(root, Path.Combine(folder, m))));
                if (!engines.TryGetValue(name, out var facts)) engines[name] = facts = [];
                foreach (var marker in markers) facts.Add(Path.GetRelativePath(root, Path.Combine(folder, marker)));
            }
            if (DirAt(stem + "_Data") && FileAt("UnityPlayer.dll")) Hint("Unity", stem + "_Data", "UnityPlayer.dll");
            if (DirAt("renpy") && DirAt("game")) Hint("Ren'Py", "renpy", "game");
            if (FileAt("www/js/rpg_core.js") || FileAt("js/rpg_core.js")) Hint("RPG Maker MV", FileAt("www/js/rpg_core.js") ? "www/js/rpg_core.js" : "js/rpg_core.js");
            if (FileAt("js/rmmz_core.js") || FileAt("www/js/rmmz_core.js")) Hint("RPG Maker MZ", FileAt("js/rmmz_core.js") ? "js/rmmz_core.js" : "www/js/rmmz_core.js");
            if (FileAt(stem + ".pck")) Hint("Godot (possible)", stem + ".pck");
            if (relative.Replace('\\', '/').Contains("/Binaries/Win64/", StringComparison.OrdinalIgnoreCase) && stem.EndsWith("-Shipping", StringComparison.OrdinalIgnoreCase))
                Hint("Unreal Engine (possible)", Path.GetFileName(exe));
            if (engine == "Unknown") evidence.Add("No supported engine markers; executable presence alone is weak evidence.");
            candidates.Add(new(exe, engine, rank, evidence.ToArray()));
        }
        if (engines.Count > 1) notes.Add("Multiple engine signatures found. They may belong to bundled games or tools; review the paths.");
        if (candidates.Count > 1) notes.Add("Multiple launch candidates. Ranking is heuristic, not a probability or an automatic choice.");
        return new(installation.Id, root, candidates.OrderByDescending(c => c.Rank).ThenBy(c => c.Path, StringComparer.OrdinalIgnoreCase).ToArray(),
            engines.Select(e => new EngineSuggestion(e.Key, e.Value.Order().ToArray())).OrderBy(e => e.Name).ToArray(), notes);
    }
    public static Library Apply(Library library, Guid gameId, DetectionReport report, string? candidatePath, string? engine, string? title)
    {
        var copy = JsonSerializer.Deserialize<Library>(JsonSerializer.Serialize(library))!;
        var game = copy.Games.Single(g => g.Id == gameId);
        var installation = game.Installations.Single(i => i.Id == report.InstallationId);
        if (!Game.SamePath(installation.Path, report.Root)) throw new InvalidOperationException("Installation changed. Scan again.");
        var evidence = new List<string>();
        if (candidatePath is not null)
        {
            var candidate = report.Candidates.Single(c => c.Path == candidatePath);
            if (IsHelper(candidate.Path)) throw new InvalidOperationException("Helper executables are not suggestions.");
            var profile = new LaunchProfile { Name = "Detected: " + Path.GetFileNameWithoutExtension(candidate.Path), ExecutablePath = candidate.Path, WorkingDirectory = Path.GetDirectoryName(candidate.Path)! };
            if (!installation.Profiles.Any(p => Game.SamePath(string.IsNullOrWhiteSpace(p.ExecutablePath) ? installation.Path : Path.GetFullPath(p.ExecutablePath, installation.Path), candidate.Path)))
            {
                var first = installation.Profiles.Count == 0; installation.Profiles.Add(profile); LaunchService.Prepare(installation, profile);
                if (first) installation.DefaultProfileId = profile.Id;
            }
            evidence.AddRange(candidate.Evidence);
        }
        if (engine is not null)
        {
            var suggestion = report.Engines.Single(e => e.Name == engine);
            MetadataReconciliation.SetManual(game, MetadataField.Engine, engine);
            evidence.AddRange(suggestion.Evidence);
        }
        if (title is not null)
        {
            var suggested = FilenameParser.FromLocation(installation.Path, true).Title;
            if (title != suggested || string.IsNullOrWhiteSpace(title)) throw new InvalidOperationException("Title suggestion changed. Review again.");
            MetadataReconciliation.SetManual(game, MetadataField.Title, title); evidence.Add("Title from installation path: " + installation.Path);
        }
        if (candidatePath is not null || engine is not null || title is not null)
        {
            game.DetectionHistory.Add(new(DateTimeOffset.UtcNow, report.Root, candidatePath ?? "", engine ?? "", evidence.Distinct().ToArray()));
            if (game.DetectionHistory.Count > 20) game.DetectionHistory.RemoveAt(0);
        }
        return copy;
    }
}
