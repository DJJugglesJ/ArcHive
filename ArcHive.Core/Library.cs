using System.Text.Json;

namespace ArcHive.Core;

public sealed class Game : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    private bool isSelected;
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsSelected { get => isSelected; set { if (isSelected == value) return; isSelected = value; PropertyChanged?.Invoke(this, new(nameof(IsSelected))); } }
    public F95ThreadLink? F95Thread { get; set; }
    public Dictionary<string, string> OwnedLibraryIds { get; set; } = [];
    public List<SaveLocation> SaveLocations { get; set; } = [];
    public List<DetectionDecision> DetectionHistory { get; set; } = [];
    [System.Text.Json.Serialization.JsonIgnore]
    public string SourceLocation => Installations.FirstOrDefault()?.Path ?? Archives.FirstOrDefault()?.Path ?? DirectoryPath ?? ArchivePath ?? "No source linked";
    [System.Text.Json.Serialization.JsonIgnore]
    public string CompactSource => DisplayPaths.Compact(SourceLocation);
    [System.Text.Json.Serialization.JsonIgnore]
    public string CompactSourceSummary => string.Join("\n", Installations.Select(i => "Folder: " + DisplayPaths.Compact(i.Path)).Concat(Archives.Select(a => "Archive: " + DisplayPaths.Compact(a.Path))));
    [System.Text.Json.Serialization.JsonIgnore]
    public string SourceSummary => string.Join("\n", Installations.Select(i => "Folder: " + i.Path).Concat(Archives.Select(a => "Archive: " + a.Path)));
    public GameArtwork Artwork { get; set; } = new();
    public ControllerSupport ControllerSupport { get; set; } = ControllerSupport.Unknown;
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasControllerSupport => ControllerSupport is ControllerSupport.Full or ControllerSupport.Partial;
    public MetadataState Metadata { get; set; } = new();
    public string ReleaseYear { get; set; } = "";
    public string Engine { get; set; } = "";
    public List<GameArchive> Archives { get; set; } = [];
    public List<GameInstallation> Installations { get; set; } = [];
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string? ArchivePath { get; set; }
    public string? DirectoryPath { get; set; }
    public string? ExecutablePath { get; set; }
    public string Tags { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsNsfw { get; set; }
    public bool Favorite { get; set; }
    public bool Played { get; set; }
    private GameProgress progress = GameProgress.Backlog;
    public GameProgress Progress { get => progress; set { progress = value; HasExplicitProgress = true; } }
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasExplicitProgress { get; private set; }
    public ReleaseInfo LatestRelease { get; set; } = new();
    public string LatestReleaseSource { get; set; } = "Manual";
    public DateTimeOffset? LatestReleaseCheckedAt { get; set; }
    public List<ReleaseObservation> ReleaseHistory { get; set; } = [];
    [System.Text.Json.Serialization.JsonIgnore]
    public string UpdateState => VersionTracking.GetUpdateState(this);
    [System.Text.Json.Serialization.JsonIgnore]
    public string CardStatus => $"{Progress} · {State}";
    public DateTimeOffset? LastLaunched { get; set; }
    public string? ScreenshotPath { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string State => Installations.Any(i => i.Profiles.Any(p => LaunchService.CanLaunch(i, p))) ? "Ready"
        : Installations.Any(i => Directory.Exists(i.Path)) || Directory.Exists(DirectoryPath) ? "Folder linked"
        : Archives.Any(a => File.Exists(a.Path)) || File.Exists(ArchivePath) ? "Archived" : OwnedLibraryIds.Count > 0 ? "Not installed" : "Missing";

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
    public ReleaseInfo Release { get; set; } = new();
    public override string ToString() => DisplayPaths.Compact(Path);
}
public sealed class GameInstallation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Path { get; set; } = "";
    public string? ExecutablePath { get; set; }
    public Guid? SourceArchiveId { get; set; }
    public List<LaunchProfile> Profiles { get; set; } = [];
    public Guid? DefaultProfileId { get; set; }
    public ReleaseInfo Release { get; set; } = new();
    public override string ToString() => DisplayPaths.Compact(Path);
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
