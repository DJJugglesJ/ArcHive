using System.Text.RegularExpressions;
namespace ArcHive.Core;

public static class NamingTemplates
{
    public static string Render(string template, string title, string source, ReleaseInfo release)
    {
        if (template.Length > 500 || !template.Contains("{title}", StringComparison.Ordinal))
            throw new ArgumentException("Include {title} in a naming template of at most 500 characters.");
        var parsed = FilenameParser.Parse(Path.GetFileName(Path.TrimEndingDirectorySeparator(source)));
        var values = new Dictionary<string, string>
        {
            ["title"] = title,
            ["version"] = string.IsNullOrWhiteSpace(release.Version) ? parsed.Version ?? "" : release.Version,
            ["platform"] = string.IsNullOrWhiteSpace(release.Platform) ? parsed.Platform ?? "" : release.Platform,
            ["build"] = parsed.Build ?? ""
        };
        var result = Regex.Replace(template, @"\{([^{}]*)\}", match =>
        {
            var key = match.Groups[1].Value;
            if (!values.TryGetValue(key, out var value)) throw new ArgumentException("Unknown naming field: " + key);
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("No " + key + " is known for " + Path.GetFileName(source) + ". Edit the template or review its release information.");
            return value;
        });
        if (result.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || result.Contains('{') || result.Contains('}') ||
            result.TrimEnd(' ', '.') != result || result.Length > 120 || string.IsNullOrWhiteSpace(result) || result is "." or ".." ||
            Regex.IsMatch(result, @"^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(?:\.|$)", RegexOptions.IgnoreCase))
            throw new IOException("The template produces an unsafe or overly long filename: " + result);
        return result;
    }
}
