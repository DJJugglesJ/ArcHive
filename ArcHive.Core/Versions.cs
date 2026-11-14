using System.Numerics;
using System.Text.RegularExpressions;

namespace ArcHive.Core;

public enum GameProgress { Backlog, Playing, Completed, Abandoned }
public sealed record ReleaseInfo
{
    public string OriginalName { get; set; } = "";
    public string Version { get; set; } = "";
    public string Platform { get; set; } = "";
    public string Channel { get; set; } = "";
    public static ReleaseInfo FromPath(string path)
    {
        var parsed = FilenameParser.Parse(Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)));
        return new() { OriginalName = parsed.Original, Version = parsed.Version ?? "", Platform = parsed.Platform ?? "" };
    }
}
public sealed record ReleaseObservation(ReleaseInfo Release, string Source, DateTimeOffset ObservedAt);

public static class VersionTracking
{
    // Deliberately conservative: dates, letter suffixes and prerelease schemes need provider-specific rules.
    public static int? Compare(ReleaseInfo left, ReleaseInfo right)
    {
        if (!string.Equals(left.Platform.Trim(), right.Platform.Trim(), StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(left.Channel.Trim(), right.Channel.Trim(), StringComparison.OrdinalIgnoreCase)) return null;
        static BigInteger[]? Parse(string value)
        {
            value = value.Trim();
            if (value.StartsWith('v') || value.StartsWith('V')) value = value[1..];
            if (value.Length > 128 || !Regex.IsMatch(value, @"^[0-9]+(?:\.[0-9]+){0,7}$")) return null;
            return value.Split('.').Select(BigInteger.Parse).ToArray();
        }
        var a = Parse(left.Version); var b = Parse(right.Version);
        if (a is null || b is null) return null;
        for (var n = 0; n < Math.Max(a.Length, b.Length); n++)
        {
            var comparison = (n < a.Length ? a[n] : BigInteger.Zero).CompareTo(n < b.Length ? b[n] : BigInteger.Zero);
            if (comparison != 0) return comparison;
        }
        return 0;
    }
    public static string GetUpdateState(Game game)
    {
        var installed = game.Installations.Where(i => Directory.Exists(i.Path)).Select(i => i.Release).ToList();
        var candidates = game.Archives.Where(a => File.Exists(a.Path)).Select(a => a.Release).ToList();
        if (!string.IsNullOrWhiteSpace(game.LatestRelease.Version)) candidates.Add(game.LatestRelease);
        if (installed.Count == 0 || candidates.Count == 0) return "Unknown";
        bool known = false, unknown = false;
        foreach (var candidate in candidates)
        {
            var comparisons = installed.Select(i => Compare(candidate, i)).ToList();
            var comparable = comparisons.Where(c => c.HasValue).Select(c => c!.Value).ToList();
            if (comparable.Count == 0) { unknown = true; continue; }
            known = true;
            // An older side-by-side installation does not flag a release already installed elsewhere.
            if (comparable.All(c => c > 0)) return "Update available";
        }
        return known && !unknown ? "Up to date" : "Unknown";
    }
    public static void Record(Game game, ReleaseInfo release, string source)
    {
        if (string.IsNullOrWhiteSpace(release.Version)) return;
        if (game.ReleaseHistory.Any(h => h.Source == source && h.Release == release)) return;
        game.ReleaseHistory.Add(new(release with { }, source, DateTimeOffset.UtcNow));
    }
    public static void InitializeLocations(Game game, bool migratePlayed = false)
    {
        game.UpgradeLocations();
        if (migratePlayed && !game.HasExplicitProgress) game.Progress = game.Played ? GameProgress.Playing : GameProgress.Backlog;
        foreach (var a in game.Archives)
        {
            if (a.Release == new ReleaseInfo()) a.Release = ReleaseInfo.FromPath(a.Path);
            Record(game, a.Release, "Archive " + a.Id);
        }
        foreach (var i in game.Installations)
        {
            var sourceRelease = game.Archives.FirstOrDefault(a => a.Id == i.SourceArchiveId)?.Release;
            if (i.Release == new ReleaseInfo()) i.Release = sourceRelease is null ? ReleaseInfo.FromPath(i.Path) : sourceRelease with { };
            if (i.ExecutablePath is not null && i.Profiles.Count == 0)
            {
                var profile = new LaunchProfile { Name = "Default", ExecutablePath = i.ExecutablePath,
                    WorkingDirectory = Path.GetDirectoryName(i.ExecutablePath) ?? i.Path };
                i.Profiles.Add(profile); i.DefaultProfileId = profile.Id;
            }
            Record(game, i.Release, "Installation " + i.Id);
        }
    }
}
