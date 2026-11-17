namespace ArcHive.Core;
public static class AutomaticLaunch
{
    public static bool IsUntouched(GameInstallation installation) => string.IsNullOrWhiteSpace(installation.ExecutablePath) &&
        (installation.Profiles.Count == 0 || installation.Profiles.Count == 1 && installation.Profiles[0] is { Name: "Default", ExecutablePath: "", WorkingDirectory: "", AdapterId: null } p && p.Arguments.Count == 0);
    public static string? Find(string folder)
    {
        try
        {
            PathSafety.NoLinks(folder);
            var entries = Directory.EnumerateFileSystemEntries(folder).Take(513).ToArray();
            if (entries.Length > 512) return null;
            var executables=entries.Where(p=>Path.GetExtension(p).Equals(".exe",StringComparison.OrdinalIgnoreCase) && File.Exists(p) && !LaunchDetection.IsHelper(p)).ToArray();
            if (executables.Length != 1) return null;
            var exe=executables[0]; PathSafety.NoLinks(exe);
            string Key(string name) => new(FilenameParser.Parse(name).Title.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
            var name=Key(Path.GetFileName(Path.TrimEndingDirectorySeparator(folder)));
            return name.Length>=3 && name.Any(char.IsLetter) && name==Key(Path.GetFileName(exe)) ? exe : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return null; }
    }
    public static bool Apply(GameInstallation installation, string candidate)
    {
        if (!IsUntouched(installation) || Find(installation.Path) is not { } found || !Game.SamePath(found,candidate)) return false;
        var profile=new LaunchProfile { Name="Automatic", ExecutablePath=candidate, WorkingDirectory=Path.GetDirectoryName(candidate)! };
        installation.Profiles.Clear(); installation.Profiles.Add(profile); installation.DefaultProfileId=profile.Id;
        return true;
    }
}
