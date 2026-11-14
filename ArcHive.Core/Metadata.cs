using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ArcHive.Core;

public enum MetadataField { Title, Description, Tags, ReleaseYear, Engine, ControllerSupport }
public sealed record MetadataCandidate(string Provider, string Id, string Title, string[] Aliases,
    Dictionary<MetadataField, string> Fields, DateTimeOffset RetrievedAt)
{
    public ArtworkReference[] Artwork { get; init; } = [];
}
public sealed record MetadataQuery(string Title, string? Year = null, string? Engine = null);
public interface IMetadataProvider
{
    string Id { get; }
    Task<IReadOnlyList<MetadataCandidate>> SearchAsync(MetadataQuery query, CancellationToken cancellationToken);
}
public sealed record MetadataMatch(MetadataCandidate Candidate, double Score, string[] Evidence, bool Ambiguous)
{
    public override string ToString() => $"{Candidate.Title} · {Candidate.Provider}/{Candidate.Id} · {Score:P0} similarity{(Ambiguous ? " · ambiguous" : "")}\n{string.Join("; ", Evidence)}";
}
public static class MetadataMatching
{
    private static string Normalize(string value) => string.Join(" ", Regex.Matches(
        value.Normalize(NormalizationForm.FormKC).ToLowerInvariant(), @"[\p{L}\p{N}]+").Select(m => m.Value));
    private static double Similarity(string left, string right)
    {
        var a = Normalize(left); var b = Normalize(right);
        if (a.Length == 0 || b.Length == 0) return 0;
        if (a == b) return 1;
        var x = a.Split(' ').ToHashSet(); var y = b.Split(' ').ToHashSet();
        return (double)x.Intersect(y).Count() / x.Union(y).Count();
    }
    public static IReadOnlyList<MetadataMatch> Rank(MetadataQuery query, IEnumerable<MetadataCandidate> candidates)
    {
        var results = candidates.Select(c =>
        {
            MetadataValidation.Validate(c);
            var primary = Similarity(query.Title, c.Title);
            var alias = c.Aliases.Select(a => Similarity(query.Title, a)).DefaultIfEmpty(0).Max();
            var score = Math.Max(primary, alias) * .8;
            var evidence = new List<string> { $"{(alias > primary ? "Alias" : "Title")} similarity {Math.Max(primary, alias):P0}" };
            void Hint(string? requested, MetadataField field)
            {
                if (string.IsNullOrWhiteSpace(requested) || !c.Fields.TryGetValue(field, out var value) || string.IsNullOrWhiteSpace(value)) return;
                var equal = Normalize(requested) == Normalize(value);
                score += equal ? .1 : -.25; evidence.Add($"{field} {(equal ? "matches" : "conflicts")}: {value}");
            }
            Hint(query.Year, MetadataField.ReleaseYear); Hint(query.Engine, MetadataField.Engine);
            return new MetadataMatch(c, Math.Clamp(score, 0, 1), evidence.ToArray(), false);
        }).GroupBy(m => (m.Candidate.Provider, m.Candidate.Id)).Select(g => g.OrderByDescending(m => m.Score).First())
          .OrderByDescending(m => m.Score).ThenBy(m => m.Candidate.Provider, StringComparer.Ordinal).ThenBy(m => m.Candidate.Id, StringComparer.Ordinal).ToArray();
        return results.Select((m, i) => m with { Ambiguous = (i > 0 && results[i - 1].Score - m.Score < .1) || (i + 1 < results.Length && m.Score - results[i + 1].Score < .1) }).ToArray();
    }
}
public static class MetadataValidation
{
    public static void Validate(MetadataCandidate c)
    {
        if (c is null || string.IsNullOrWhiteSpace(c.Provider) || c.Provider.Length > 80 || string.IsNullOrWhiteSpace(c.Id) || c.Id.Length > 200 ||
            string.IsNullOrWhiteSpace(c.Title) || c.Title.Length > 500 || c.Aliases is null || c.Aliases.Length > 100 ||
            c.Aliases.Any(a => a is null || a.Length > 500) || c.Fields is null || c.RetrievedAt == default ||
            c.Fields.Any(f => !Enum.IsDefined(f.Key) || f.Value is null || f.Value.Length > 50000))
            throw new InvalidDataException("Invalid or oversized metadata candidate.");
        if (c.Artwork is null || c.Artwork.Length > 32) throw new InvalidDataException("Too many artwork candidates.");
        foreach (var art in c.Artwork)
        {
            if (art.Provider != c.Provider || art.ProviderId != c.Id) throw new InvalidDataException("Artwork identity does not match metadata.");
            ProviderArtwork.Validate(art);
        }
        if (c.Fields.TryGetValue(MetadataField.ControllerSupport, out var support) && (!Enum.TryParse<ControllerSupport>(support, out var parsed) || !Enum.IsDefined(parsed)))
            throw new InvalidDataException("Invalid controller support value.");
    }
}
public sealed class OfflineMetadataProvider(IReadOnlyList<MetadataCandidate> candidates) : IMetadataProvider
{
    public string Id => "offline";
    public static OfflineMetadataProvider Parse(string json)
    {
        if (json.Length > 2_000_000) throw new InvalidDataException("Metadata import exceeds 2 MB.");
        var entries = JsonSerializer.Deserialize<MetadataCandidate[]>(json, new JsonSerializerOptions { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } })
            ?? throw new InvalidDataException("Expected a metadata candidate array.");
        if (entries.Length > 500) throw new InvalidDataException("Import is limited to 500 candidates.");
        foreach (var c in entries) MetadataValidation.Validate(c);
        return new(entries);
    }
    public Task<IReadOnlyList<MetadataCandidate>> SearchAsync(MetadataQuery query, CancellationToken cancellationToken)
    { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(candidates); }
}

public sealed record FieldProvenance(string Value, string Provider, string ProviderId, DateTimeOffset RetrievedAt, bool Manual);
public sealed record MetadataChange(MetadataField Field, string Before, string After, FieldProvenance? Previous, FieldProvenance Applied);
public sealed class MetadataState
{
    public Dictionary<MetadataField, FieldProvenance> Fields { get; set; } = [];
    public List<List<MetadataChange>> Undo { get; set; } = [];
}
public sealed record MetadataProposal(MetadataField Field, string Current, string Proposed, FieldProvenance? Provenance)
{
    public bool Conflict => Current.Length != 0 && Current != Proposed;
}
public static class MetadataReconciliation
{
    public static string Get(Game game, MetadataField field) => field switch
    { MetadataField.Title => game.Title, MetadataField.Description => game.Description, MetadataField.Tags => game.Tags,
      MetadataField.ReleaseYear => game.ReleaseYear, MetadataField.Engine => game.Engine, MetadataField.ControllerSupport => game.ControllerSupport.ToString(), _ => throw new ArgumentOutOfRangeException(nameof(field)) };
    private static void Set(Game game, MetadataField field, string value)
    {
        switch (field)
        {
            case MetadataField.Title: game.Title = value; break;
            case MetadataField.Description: game.Description = value; break;
            case MetadataField.Tags: game.Tags = value; break;
            case MetadataField.ReleaseYear: game.ReleaseYear = value; break;
            case MetadataField.Engine: game.Engine = value; break;
            case MetadataField.ControllerSupport: game.ControllerSupport = Enum.Parse<ControllerSupport>(value); break;
            default: throw new ArgumentOutOfRangeException(nameof(field));
        }
    }
    public static void SetManual(Game game, MetadataField field, string value)
    {
        if (Get(game, field) == value) return;
        Set(game, field, value);
        game.Metadata.Fields[field] = new(value, "Manual", "", DateTimeOffset.UtcNow, true);
    }
    public static IReadOnlyList<MetadataProposal> Preview(Game game, MetadataCandidate candidate)
    {
        MetadataValidation.Validate(candidate);
        var fields = new Dictionary<MetadataField, string>(candidate.Fields) { [MetadataField.Title] = candidate.Title };
        return fields.Where(f => !string.IsNullOrWhiteSpace(f.Value)).OrderBy(f => f.Key)
            .Select(f => new MetadataProposal(f.Key, Get(game, f.Key), f.Value, game.Metadata.Fields.GetValueOrDefault(f.Key))).ToArray();
    }
    // Only explicitly selected fields are applied. Scores never authorize mutation.
    public static void Apply(Game game, MetadataCandidate candidate, IReadOnlyCollection<MetadataProposal> chosen)
    {
        var preview = Preview(game, candidate).ToDictionary(p => p.Field);
        if (chosen.Select(p => p.Field).Distinct().Count() != chosen.Count || chosen.Any(p => !preview.TryGetValue(p.Field, out var current) || current != p))
            throw new InvalidOperationException("Metadata changed since preview. Review the fields again.");
        var changes = chosen.Select(p => new MetadataChange(p.Field, p.Current, p.Proposed, p.Provenance,
            new(p.Proposed, candidate.Provider, candidate.Id, candidate.RetrievedAt, false))).ToList();
        if (changes.Count == 0) return;
        foreach (var change in changes) { Set(game, change.Field, change.After); game.Metadata.Fields[change.Field] = change.Applied; }
        game.Metadata.Undo.Add(changes);
        if (game.Metadata.Undo.Count > 20) game.Metadata.Undo.RemoveAt(0);
    }
    public static void Undo(Game game)
    {
        if (game.Metadata.Undo.Count == 0) throw new InvalidOperationException("No metadata changes to undo.");
        var changes = game.Metadata.Undo[^1];
        if (changes.Any(c => Get(game, c.Field) != c.After || game.Metadata.Fields.GetValueOrDefault(c.Field) != c.Applied))
            throw new InvalidOperationException("A field has been edited since this lookup. Undo would overwrite that edit.");
        foreach (var c in changes)
        { Set(game, c.Field, c.Before); if (c.Previous is null) game.Metadata.Fields.Remove(c.Field); else game.Metadata.Fields[c.Field] = c.Previous; }
        game.Metadata.Undo.RemoveAt(game.Metadata.Undo.Count - 1);
    }
}
