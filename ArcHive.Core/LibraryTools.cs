using System.Text.Json;
using System.Text.RegularExpressions;
namespace ArcHive.Core;

public sealed class PlaySession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset Started { get; set; }
    public DateTimeOffset Checkpoint { get; set; }
    public double Seconds { get; set; }
    public bool Active { get; set; }
    public bool NeedsReview { get; set; }
    public string Note { get; set; } = "";
    public override string ToString() => $"{Started.LocalDateTime:g} · {TimeSpan.FromSeconds(Seconds):g}" + (NeedsReview ? " · Review" : "") + (Active ? " · Playing" : "") + " · " + Note;
}
public static class Playtime
{
    public static PlaySession Begin(Game game, DateTimeOffset now)
    {
        if (game.Sessions.Any(s => s.Active)) throw new InvalidOperationException("This game already has an active play session.");
        var session = new PlaySession { Started = now, Checkpoint = now, Active = true }; game.Sessions.Add(session); return session;
    }
    public static void Tick(PlaySession session, DateTimeOffset now, double elapsedSeconds)
    {
        if (!session.Active || !double.IsFinite(elapsedSeconds) || elapsedSeconds < 0) return;
        // A sleeping machine or suspended monitor must not add unobserved hours.
        if (elapsedSeconds > 30) { session.NeedsReview = true; session.Note = "Monitoring paused; unobserved time was not counted."; }
        else session.Seconds += elapsedSeconds;
        session.Checkpoint = now;
    }
    public static void Finish(PlaySession session, bool uncertain, string note = "")
    { session.Active = false; session.NeedsReview |= uncertain; if (note.Length > 0) session.Note = note; }
    public static bool Recover(IEnumerable<Game> games)
    {
        var recovered = false;
        foreach (var s in games.SelectMany(g => g.Sessions).Where(s => s.Active))
        { Finish(s, true, "ArcHive closed during this session. Only checkpointed time was kept; review the duration."); recovered = true; }
        return recovered;
    }
    public static void Correct(PlaySession session, double minutes)
    {
        if (session.Active) throw new InvalidOperationException("Wait for the session to finish before correcting it.");
        if (!double.IsFinite(minutes) || minutes < 0 || minutes > 5256000) throw new ArgumentException("Enter minutes between 0 and 5,256,000.");
        session.Seconds = minutes * 60; session.NeedsReview = false; session.Note = "Duration corrected manually.";
    }
}
public sealed class GameNaming
{
    public string OriginalName { get; set; } = "";
    public string CanonicalTitle { get; set; } = "";
    public string SuggestedTitle { get; set; } = "";
    public string Confidence { get; set; } = "Unknown";
    public string[] Evidence { get; set; } = [];
    public bool Reviewed { get; set; }
}
public static class NamingReview
{
    public static GameNaming Suggest(Game game)
    {
        var path = game.SourceLocation; var parsed = FilenameParser.FromLocation(path, game.Installations.Count > 0);
        var evidence = new List<string> { "Source: " + path, "Bare numbers and date-like tokens remain part of the title." };
        if (parsed.Version is not null) evidence.Add("Explicit version token: " + parsed.Version);
        if (parsed.Platform is not null) evidence.Add("Explicit platform token: " + parsed.Platform);
        var ambiguous = !parsed.Title.Any(char.IsLetter) || Regex.IsMatch(parsed.Title, @"\b\d{4}[-._ ]\d{1,2}[-._ ]\d{1,2}\b");
        evidence.Add("CamelCase and separator cleanup is a suggestion; it does not identify the game.");
        return new() { OriginalName = string.IsNullOrEmpty(game.Naming.OriginalName) ? parsed.Original : game.Naming.OriginalName, CanonicalTitle = game.Naming.CanonicalTitle, SuggestedTitle = parsed.Title, Confidence = ambiguous ? "Low" : "Medium", Evidence = evidence.ToArray() };
    }
    public static void Apply(Game game, GameNaming proposal, string canonical, bool changeDisplay)
    {
        if (string.IsNullOrWhiteSpace(canonical) || canonical.Length > 500) throw new ArgumentException("Enter a title of 1–500 characters.");
        game.Naming = JsonSerializer.Deserialize<GameNaming>(JsonSerializer.Serialize(proposal))!; game.Naming.CanonicalTitle = canonical.Trim(); game.Naming.Reviewed = true;
        if (changeDisplay) MetadataReconciliation.SetManual(game, MetadataField.Title, canonical.Trim());
    }
}
public sealed record BrowseOptions(string Collection = "", string Tag = "", string Engine = "", bool ControllerOnly = false, bool UnplayedOnly = false, string Installation = "", string Sort = "Library Settings");
public sealed record LibraryStatistics(int Games, int Favorites, int Played, int Completed, double Hours);
public static class LibraryBrowsing
{
    public static IEnumerable<Game> Apply(IEnumerable<Game> games, BrowseOptions options)
    {
        var query = games.Where(g => (options.Collection.Length == 0 || g.Collections.Contains(options.Collection, StringComparer.OrdinalIgnoreCase)) &&
            (options.Tag.Length == 0 || g.Tags.Split(',').Any(t => t.Trim().Equals(options.Tag, StringComparison.OrdinalIgnoreCase))) &&
            (options.Engine.Length == 0 || g.Engine.Equals(options.Engine, StringComparison.OrdinalIgnoreCase)) &&
            (!options.ControllerOnly || g.HasControllerSupport) && (!options.UnplayedOnly || !g.Played) &&
            (options.Installation.Length == 0 || g.InstallationStatus == options.Installation));
        return options.Sort switch { "Playtime" => query.OrderByDescending(g => g.PlaytimeSeconds).ThenBy(g => g.Title), "Recently Played" => query.OrderByDescending(g => g.LastLaunched).ThenBy(g => g.Title), "Title" => query.OrderBy(g => g.Title, StringComparer.OrdinalIgnoreCase), _ => query };
    }
    public static LibraryStatistics Statistics(IEnumerable<Game> games)
    { var list = games.ToArray(); return new(list.Length, list.Count(g => g.Favorite), list.Count(g => g.Played), list.Count(g => g.Progress == GameProgress.Completed), list.Sum(g => g.PlaytimeSeconds) / 3600); }
}
public sealed record MergePreview(Library Result, Guid PrimaryId, string Fingerprint, string[] Summary);
public static class DuplicateMerge
{
    public static string Fingerprint(Library library) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(library)));
    public static MergePreview Preview(Library source, IEnumerable<Guid> selected, Guid primaryId)
    {
        var ids = selected.ToHashSet();
        if (ids.Count < 2 || !ids.Contains(primaryId) || ids.Any(id => source.Games.All(g => g.Id != id))) throw new ArgumentException("Select at least two games and a primary game from that selection.");
        if (source.Games.Where(g => ids.Contains(g.Id)).Any(g => g.Sessions.Any(s => s.Active))) throw new InvalidOperationException("Finish active play sessions before merging games.");
        var copy = JsonSerializer.Deserialize<Library>(JsonSerializer.Serialize(source))!; var primary = copy.Games.Single(g => g.Id == primaryId);
        var summary = new List<string> { "Keep primary title, metadata, status, artwork and launch defaults: " + primary.Title, "Keep all source records for review; retain original files and save-backup ownership." };
        foreach (var other in copy.Games.Where(g => ids.Contains(g.Id) && g.Id != primaryId).ToArray())
        {
            primary.MergedRecords.Add(JsonSerializer.Deserialize<Game>(JsonSerializer.Serialize(other))!);
            // Keep association IDs and defaults intact, even for identical paths. These may describe different launch configurations.
            primary.Archives.AddRange(other.Archives); primary.Installations.AddRange(other.Installations);
            foreach (var location in other.SaveLocations) { location.BackupOwnerId ??= other.Id; primary.SaveLocations.Add(location); }
            primary.Sessions.AddRange(other.Sessions.Where(s => primary.Sessions.All(p => p.Id != s.Id)));
            primary.Collections = primary.Collections.Concat(other.Collections).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            primary.Favorite |= other.Favorite; primary.Played |= other.Played; primary.IsNsfw |= other.IsNsfw;
            if (other.LastLaunched > (primary.LastLaunched ?? DateTimeOffset.MinValue)) primary.LastLaunched = other.LastLaunched;
            summary.Add($"{other.Title}: retain {other.Archives.Count} archives, {other.Installations.Count} installations, {other.SaveLocations.Count} save locations and {other.Sessions.Count} sessions. Conflicting fields remain in the preserved source record.");
            copy.Games.Remove(other);
        }
        return new(copy, primaryId, Fingerprint(source), summary.ToArray());
    }
    public static Library Apply(Library current, MergePreview preview)
    { if (Fingerprint(current) != preview.Fingerprint) throw new InvalidOperationException("The library changed. Preview the merge again."); return preview.Result; }
}
