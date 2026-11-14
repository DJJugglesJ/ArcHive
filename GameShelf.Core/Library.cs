using System.Text.Json;

namespace GameShelf.Core;

public sealed class Game
{
    public List<GameArchive> Archives { get; set; } = [];
    public List<GameInstallation> Installations { get; set; } = [];
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string? ArchivePath { get; set; }
    public string? DirectoryPath { get; set; }
    public string? ExecutablePath { get; set; }
    public string Tags { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Favorite { get; set; }
    public bool Played { get; set; }
    public DateTimeOffset? LastLaunched { get; set; }
    public string? ScreenshotPath { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string State => Installations.Any(i => Directory.Exists(i.Path)) || Directory.Exists(DirectoryPath)
        ? "Extracted / folder" : Archives.Any(a => File.Exists(a.Path)) || File.Exists(ArchivePath) ? "Archive only" : "Missing";

    public void UpgradeLocations()
    {
        if (ArchivePath is not null && !Archives.Any(a => SamePath(a.Path, ArchivePath))) Archives.Add(new() { Path = ArchivePath });
        if (DirectoryPath is not null && !Installations.Any(i => SamePath(i.Path, DirectoryPath)))
            Installations.Add(new() { Path = DirectoryPath, ExecutablePath = ExecutablePath });
        ArchivePath = DirectoryPath = ExecutablePath = null;
    }
    public static bool SamePath(string a, string b) => string.Equals(System.IO.Path.GetFullPath(a).TrimEnd('\\'), System.IO.Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
}

public sealed class GameArchive
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Path { get; set; } = "";
    public override string ToString() => Path;
}
public sealed class GameInstallation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Path { get; set; } = "";
    public string? ExecutablePath { get; set; }
    public Guid? SourceArchiveId { get; set; }
    public override string ToString() => Path;
}

public sealed class Library
{
    public int Version { get; set; } = 1;
    public List<string> Roots { get; set; } = [];
    public List<Game> Games { get; set; } = [];
}

public sealed class LibraryStore(string file)
{
    public Library Load()
    {
        if (!File.Exists(file)) return new();
        var library = JsonSerializer.Deserialize<Library>(File.ReadAllText(file))
            ?? throw new InvalidDataException("The library file is empty or invalid.");
        if (library.Version != 1) throw new InvalidDataException("Unsupported library version.");
        return library;
    }

    public void Save(Library library)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var temporary = file + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, library, new JsonSerializerOptions { WriteIndented = true });
            stream.Flush(true);
        }
        if (File.Exists(file)) File.Replace(temporary, file, file + ".bak");
        else File.Move(temporary, file);
    }
}
