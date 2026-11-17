namespace ArcHive.Core;
public static class GameTags
{
    public static string[] Parse(string? text) => (text ?? "").Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
        .Select(t => t.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
    public static string Normalize(string? text) => string.Join(", ", Parse(text));
    public static bool Contains(string? text, string tag) => Parse(text).Contains(tag, StringComparer.OrdinalIgnoreCase);
}
