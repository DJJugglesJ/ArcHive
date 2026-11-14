namespace ArcHive.Core;

public static class DisplayPaths
{
    // Presentation only: never feed shortened strings into filesystem or launch operations.
    public static string Compact(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "No source linked";
        try
        {
            var normalized = path.Replace('/', '\\').TrimEnd('\\');
            if (normalized.Length == 2 && normalized[1] == ':') return normalized + "\\";
            var root = Path.GetPathRoot(normalized) ?? "";
            if (root.Length == 0) return normalized;
            var parts = normalized[root.TrimEnd('\\').Length..].TrimStart('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= 2) return normalized;
            return root.TrimEnd('\\') + "\\…\\" + string.Join("\\", parts.TakeLast(2));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException) { return path; }
    }
}
public enum LibraryAcceptAction { OpenDetails, ActivateDetailAction }
public static class LibraryInteraction
{
    public static LibraryAcceptAction Accept(bool detailsPage) => detailsPage ? LibraryAcceptAction.ActivateDetailAction : LibraryAcceptAction.OpenDetails;
}
