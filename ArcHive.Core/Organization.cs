using System.Security.Cryptography;
using System.Text.Json;

namespace ArcHive.Core;

public enum DuplicateKind { SharedPath, ExactArchive, PossibleTitle }
public sealed record DuplicateMember(Guid GameId, string Title, string Path);
public sealed record DuplicateGroup(DuplicateKind Kind, string Evidence, IReadOnlyList<DuplicateMember> Members)
{
    public string Label => $"{Kind}: {Members.Count} associations - {Evidence}";
}
public sealed record DuplicateReport(IReadOnlyList<DuplicateGroup> Groups, IReadOnlyList<string> Warnings);

public static class DuplicateDetection
{
    // Read-only scan. Hash archive candidates of equal size; never hash executables as game identity.
    public static async Task<DuplicateReport> ScanAsync(IEnumerable<Game> games, bool hashArchives,
        IProgress<string>? progress = null, CancellationToken token = default)
    {
        var snapshot = games.ToArray(); var groups = new List<DuplicateGroup>(); var warnings = new List<string>();
        var paths = new Dictionary<string, List<DuplicateMember>>(StringComparer.OrdinalIgnoreCase);
        var archives = new Dictionary<string, List<DuplicateMember>>(StringComparer.OrdinalIgnoreCase);
        foreach (var game in snapshot)
        {
            token.ThrowIfCancellationRequested();
            foreach (var location in game.Archives.Select(a => (a.Path, Archive: true)).Concat(game.Installations.Select(i => (i.Path, Archive: false))))
            {
                try
                {
                    if (!Path.IsPathFullyQualified(location.Path)) throw new IOException("Path is not absolute.");
                    var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(location.Path));
                    var member = new DuplicateMember(game.Id, game.Title, path);
                    if (!paths.TryGetValue(path, out var entries)) paths[path] = entries = [];
                    entries.Add(member);
                    if (location.Archive)
                    {
                        if (!archives.TryGetValue(path, out var references)) archives[path] = references = [];
                        references.Add(member);
                    }
                }
                catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException)
                { warnings.Add($"{game.Title}: invalid location: {ex.Message}"); }
            }
        }
        foreach (var pair in paths.Where(p => p.Value.Count > 1))
            groups.Add(new(DuplicateKind.SharedPath, pair.Key, pair.Value));
        foreach (var group in snapshot.GroupBy(g => FilenameParser.Parse(g.Title).Title.Trim(), StringComparer.OrdinalIgnoreCase)
                     .Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() > 1))
            groups.Add(new(DuplicateKind.PossibleTitle, group.Key + " (may be different releases; not proof of identical content)",
                group.Select(g => new DuplicateMember(g.Id, g.Title, "")).ToArray()));
        if (hashArchives)
        {
            var sizes = new Dictionary<long, List<string>>();
            foreach (var path in archives.Keys)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    PathSafety.NoLinks(path);
                    var size = new FileInfo(path).Length;
                    if (!sizes.TryGetValue(size, out var list)) sizes[size] = list = [];
                    list.Add(path);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { warnings.Add($"{path}: {ex.Message}"); }
            }
            foreach (var bucket in sizes.Where(p => p.Value.Count > 1))
            {
                var hashes = new Dictionary<string, List<DuplicateMember>>();
                foreach (var path in bucket.Value)
                {
                    token.ThrowIfCancellationRequested(); progress?.Report("Checking archive: " + path);
                    try
                    {
                        PathSafety.NoLinks(path);
                        var before = File.GetLastWriteTimeUtc(path);
                        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
                        if (stream.Length != bucket.Key) throw new IOException("Archive changed during scan; rescan to compare.");
                        var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, token));
                        if (stream.Length != bucket.Key || before != File.GetLastWriteTimeUtc(path)) throw new IOException("Archive changed during scan; rescan to compare.");
                        if (!hashes.TryGetValue(hash, out var members)) hashes[hash] = members = [];
                        members.AddRange(archives[path]);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    { warnings.Add($"{path}: {ex.Message}"); }
                }
                foreach (var pair in hashes.Where(p => p.Value.Select(m => m.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1))
                    groups.Add(new(DuplicateKind.ExactArchive, $"{bucket.Key:N0} bytes / SHA-256 {pair.Key}", pair.Value));
            }
        }
        token.ThrowIfCancellationRequested();
        return new(groups.OrderBy(g => g.Kind).ThenBy(g => g.Evidence, StringComparer.OrdinalIgnoreCase).ToArray(), warnings);
    }
}

public enum BatchAction { Favorite, Unfavorite, SetStatus, AddTags, RemoveTags, MarkNsfw, ClearNsfw, RemoveEntries, AddCollection, RemoveCollection, MarkPlayed, MarkUnplayed }
public sealed record BatchRequest(BatchAction Action, GameProgress Status = GameProgress.Backlog, string Tags = "");
public sealed record BatchChange(Guid GameId, string Title, string Before, string After);
public sealed record BatchPlan(Library Result, IReadOnlyList<BatchChange> Changes);

public static class BatchOperations
{
    private static List<string> Tags(string text) => text.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    // Plan on a deep copy, allowing one transactional save before replacing the live library.
    public static BatchPlan Preview(Library source, IEnumerable<Guid> selectedIds, BatchRequest request)
    {
        if (!Enum.IsDefined(request.Action) || !Enum.IsDefined(request.Status)) throw new ArgumentException("Unknown batch action or status.");
        var ids = selectedIds.ToHashSet();
        if (ids.Count == 0) throw new ArgumentException("Select at least one entry.");
        if (ids.Any(id => !source.Games.Any(g => g.Id == id))) throw new InvalidOperationException("Selection changed. Reopen batch organization.");
        var tags = Tags(request.Tags);
        if (request.Action is BatchAction.AddTags or BatchAction.RemoveTags or BatchAction.AddCollection or BatchAction.RemoveCollection && (tags.Count == 0 || request.Tags.Length > 4096))
            throw new ArgumentException("Enter comma-separated tags (up to 4096 characters).");
        var copy = JsonSerializer.Deserialize<Library>(JsonSerializer.Serialize(source))!;
        var changes = new List<BatchChange>();
        foreach (var game in copy.Games.Where(g => ids.Contains(g.Id)).ToArray())
        {
            string before, after;
            switch (request.Action)
            {
                case BatchAction.Favorite: case BatchAction.Unfavorite:
                    before = game.Favorite.ToString(); game.Favorite = request.Action == BatchAction.Favorite; after = game.Favorite.ToString(); break;
                case BatchAction.SetStatus:
                    before = game.Progress.ToString(); game.Progress = request.Status; after = game.Progress.ToString(); break;
                case BatchAction.AddTags: case BatchAction.RemoveTags:
                    before = game.Tags; var current = Tags(game.Tags);
                    after = string.Join(", ", request.Action == BatchAction.AddTags ? current.Concat(tags).Distinct(StringComparer.OrdinalIgnoreCase) : current.Except(tags, StringComparer.OrdinalIgnoreCase));
                    MetadataReconciliation.SetManual(game, MetadataField.Tags, after); break;
                case BatchAction.AddCollection: case BatchAction.RemoveCollection:
                    before = string.Join(", ", game.Collections);
                    game.Collections = (request.Action == BatchAction.AddCollection ? game.Collections.Concat(tags).Distinct(StringComparer.OrdinalIgnoreCase) : game.Collections.Except(tags, StringComparer.OrdinalIgnoreCase)).ToList();
                    after = string.Join(", ", game.Collections); break;
                case BatchAction.MarkPlayed: case BatchAction.MarkUnplayed:
                    before = game.Played.ToString(); game.Played = request.Action == BatchAction.MarkPlayed; after = game.Played.ToString(); break;
                case BatchAction.MarkNsfw: case BatchAction.ClearNsfw:
                    before = game.IsNsfw.ToString(); game.IsNsfw = request.Action == BatchAction.MarkNsfw; after = game.IsNsfw.ToString(); break;
                case BatchAction.RemoveEntries:
                    if (game.Sessions.Any(s => s.Active)) throw new InvalidOperationException("Finish active play sessions before removing a game.");
                    before = $"{game.Archives.Count} archives / {game.Installations.Count} installations linked";
                    after = "Remove library entry only; files retained"; copy.Games.Remove(game); break;
                default: throw new ArgumentException("Unknown action.");
            }
            changes.Add(new(game.Id, game.Title, before, after));
        }
        return new(copy, changes);
    }
}
