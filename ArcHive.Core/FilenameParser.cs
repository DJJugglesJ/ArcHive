using System.Text.RegularExpressions;

namespace ArcHive.Core;

public sealed record ParsedGameName(string Original, string Title, string? Version, string? Platform);
public static class FilenameParser
{
    // Keep numeric identifiers, but include immediate parent context on new discoveries.
    public static ParsedGameName FromLocation(string path, bool directory)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        var parsed = Parse(name);
        if (parsed.Title.Any(char.IsLetter)) return parsed;
        var parent = Path.GetFileName(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(path)));
        var context = Parse(parent ?? "").Title;
        return context.Any(char.IsLetter) ? parsed with { Title = context + " · " + parsed.Title } : parsed;
    }
    public static ParsedGameName Parse(string name)
    {
        var original = name;
        var extension = Path.GetExtension(name);
        if (new[] { ".zip", ".rar", ".7z", ".exe" }.Contains(extension, StringComparer.OrdinalIgnoreCase))
            name = name[..^extension.Length];
        var version = Regex.Match(name, @"(?i)(?<![a-z0-9])v(?:er(?:sion)?)?[ ._-]?(\d+(?:\.\d+)*[a-z]?)(?![a-z0-9])");
        var platform = Regex.Match(name, @"(?i)(?<![a-z0-9])(win(?:dows)?(?:32|64)?|linux|mac(?:os)?)(?![a-z0-9])");
        var title = name;
        // Remove only explicit release/platform tokens. Bare numbers remain part of the title.
        foreach (var match in new[] { version, platform }.Where(m => m.Success).OrderByDescending(m => m.Index))
            title = title.Remove(match.Index, match.Length);
        title = Regex.Replace(title, @"\[\s*\]|\(\s*\)", " ");
        title = Regex.Replace(title, @"([A-Z]+)([A-Z][a-z])", "$1 $2");
        title = Regex.Replace(title, @"([a-z0-9])([A-Z])", "$1 $2");
        title = Regex.Replace(title, @"[_\s]+", " ").Trim(' ', '-', '.');
        if (string.IsNullOrWhiteSpace(title)) title = name;
        return new(original, title, version.Success ? version.Groups[1].Value : null,
            platform.Success ? platform.Value.ToLowerInvariant() switch
            {
                "win64" or "windows64" => "Windows x64",
                "win32" or "windows32" => "Windows x86",
                "win" or "windows" => "Windows",
                "linux" => "Linux",
                _ => "macOS"
            } : null);
    }
}
