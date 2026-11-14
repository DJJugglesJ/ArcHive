namespace GameShelf.Core;

public sealed record ScanResult(List<Game> Games, List<string> Warnings);

public static class Scanner
{
    public static ScanResult Scan(string root, CancellationToken cancellationToken)
    {
        root = Path.GetFullPath(root);
        PathSafety.NoLinks(root);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        var games = new List<Game>();
        var warnings = new List<string>();
        var seenFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Walk(string directory)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                foreach (var file in Directory.EnumerateFiles(directory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) continue;
                    var extension = Path.GetExtension(file).ToLowerInvariant();
                    if (extension is ".zip" or ".rar" or ".7z")
                        games.Add(new Game { Title = FilenameParser.Parse(Path.GetFileName(file)).Title, ArchivePath = file });
                    if (extension == ".exe" && seenFolders.Add(directory))
                        games.Add(new Game { Title = FilenameParser.Parse(Path.GetFileName(directory)).Title, DirectoryPath = directory });
                }
                foreach (var child in Directory.EnumerateDirectories(directory))
                    if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) Walk(child);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            { warnings.Add($"{directory}: {ex.Message}"); }
        }
        Walk(root);
        return new(games, warnings);
    }

    public static void Merge(Library library, IEnumerable<Game> discoveries)
    {
        foreach (var item in discoveries)
        {
            foreach (var game in library.Games) game.UpgradeLocations();
            item.UpgradeLocations();
            // Never merge by title: archives and folders can have unrelated contents.
            if (library.Games.Any(g => item.Archives.Count > 0
                ? g.Archives.Any(a => Game.SamePath(a.Path, item.Archives[0].Path))
                : g.Installations.Any(i => PathSafety.IsWithin(i.Path, item.Installations[0].Path)))) continue;
            library.Games.Add(item);
        }
    }
}
