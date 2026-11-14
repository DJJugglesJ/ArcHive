using System.Text.Json;

namespace ArcHive.Core;

public enum LibraryOrder { Title, RecentlyPlayed, FavoritesFirst }
public enum LibraryDensity { Comfortable, Compact }
public enum ControllerProfile { DualSense, DualShock4, Xbox }
public sealed class AppPreferences
{
    public LibraryOrder LibraryOrder { get; set; }
    public LibraryDensity LibraryDensity { get; set; }
    public bool EnableF95Zone { get; set; }
    public ControllerProfile ControllerProfile { get; set; } = ControllerProfile.DualSense;
    public bool ShowNsfw { get; set; }
    public bool GamepadEnabled { get; set; } = true;
    public bool Includes(Game game) => ShowNsfw || !game.IsNsfw;
}
public sealed record ControllerPrompts(string Accept, string Cancel, string Alternate, string Menu, string Previous, string Next)
{
    public static ControllerPrompts For(ControllerProfile profile) => profile switch
    {
        ControllerProfile.DualSense or ControllerProfile.DualShock4 => new("Cross", "Circle", "Square", "Triangle", "L1", "R1"),
        ControllerProfile.Xbox => new("A", "B", "X", "Y", "LB", "RB"),
        _ => throw new ArgumentOutOfRangeException(nameof(profile))
    };
}
public sealed class AppPreferencesStore(string file)
{
    public AppPreferences Load()
    {
        if (!File.Exists(file)) return new();
        var settings = JsonSerializer.Deserialize<AppPreferences>(File.ReadAllText(file)) ?? throw new InvalidDataException("Invalid app settings.");
        if (!Enum.IsDefined(settings.ControllerProfile) || !Enum.IsDefined(settings.LibraryOrder) || !Enum.IsDefined(settings.LibraryDensity)) throw new InvalidDataException("Unsupported controller profile.");
        return settings;
    }
    public void Save(AppPreferences settings)
    {
        if (!Enum.IsDefined(settings.ControllerProfile) || !Enum.IsDefined(settings.LibraryOrder) || !Enum.IsDefined(settings.LibraryDensity)) throw new InvalidDataException("Unsupported controller profile.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        var temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { JsonSerializer.Serialize(stream, settings); stream.Flush(true); }
            if (File.Exists(file)) File.Replace(temporary, file, file + ".bak"); else File.Move(temporary, file);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
