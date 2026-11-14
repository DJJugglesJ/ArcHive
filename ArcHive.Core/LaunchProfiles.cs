using System.Diagnostics;
using System.Text;

namespace ArcHive.Core;

public sealed class LaunchProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Default";
    public string ExecutablePath { get; set; } = "";
    public List<string> Arguments { get; set; } = [];
    public string WorkingDirectory { get; set; } = "";
    public string? AdapterId { get; set; }
    public override string ToString() => Name;
}
public sealed record LaunchPlan(string Executable, string WorkingDirectory, IReadOnlyList<string> Arguments)
{
    public string CommandLine => string.Join(" ", new[] { Executable }.Concat(Arguments).Select(LaunchService.QuoteArgument));
    public ProcessStartInfo ToStartInfo()
    {
        var info = new ProcessStartInfo(Executable) { UseShellExecute = false, WorkingDirectory = WorkingDirectory };
        foreach (var argument in Arguments) info.ArgumentList.Add(argument);
        return info;
    }
}
public static class LaunchService
{
    public static LaunchPlan Prepare(GameInstallation installation, LaunchProfile profile)
    {
        if (!installation.Profiles.Any(p => p.Id == profile.Id)) throw new InvalidDataException("The profile does not belong to this installation.");
        if (!string.IsNullOrWhiteSpace(profile.AdapterId)) throw new NotSupportedException("This launch adapter is not installed. Choose a native profile.");
        return PrepareNative(installation, profile);
    }
    public static LaunchPlan PrepareNative(GameInstallation installation, LaunchProfile profile)
    {
        if (!installation.Profiles.Any(p => p.Id == profile.Id)) throw new InvalidDataException("The profile does not belong to this installation.");
        var root = Path.GetFullPath(installation.Path);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException("The installation folder is unavailable.");
        if (string.IsNullOrWhiteSpace(profile.ExecutablePath)) throw new FileNotFoundException("Choose an executable for this profile.");
        var exe = Path.GetFullPath(profile.ExecutablePath, root);
        var working = string.IsNullOrWhiteSpace(profile.WorkingDirectory) ? Path.GetDirectoryName(exe)!
            : Path.GetFullPath(profile.WorkingDirectory, root);
        if (!PathSafety.IsWithin(root, exe) || !exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            throw new IOException("The executable must be an .exe inside this installation.");
        if (!Game.SamePath(root, working) && !PathSafety.IsWithin(root, working))
            throw new IOException("The working directory must be inside this installation.");
        PathSafety.NoLinks(root); PathSafety.NoLinks(exe); PathSafety.NoLinks(working);
        if (!File.Exists(exe)) throw new FileNotFoundException("The selected executable is missing.");
        if (!Directory.Exists(working)) throw new DirectoryNotFoundException("The working directory is missing.");
        if (profile.Arguments.Any(a => a is null || a.Contains('\0'))) throw new InvalidDataException("Arguments cannot contain null characters.");
        return new(exe, working, profile.Arguments.ToArray());
    }
    public static async Task<LaunchPlan> PrepareAsync(GameInstallation installation, LaunchProfile profile, IEnumerable<ILaunchAdapter> adapters, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var native = PrepareNative(installation, profile);
        if (string.IsNullOrWhiteSpace(profile.AdapterId)) return native;
        var id = profile.AdapterId.Split('/')[0];
        var adapter = adapters.SingleOrDefault(a => a.Id == id) ?? throw new NotSupportedException("The selected launch adapter is unavailable or disabled. Check Settings → Integrations.");
        return await adapter.PrepareAsync(native, profile, token);
    }
    public static bool CanConfigure(GameInstallation installation, LaunchProfile profile)
    {
        try { PrepareNative(installation, profile); return true; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { return false; }
    }
    public static bool CanLaunch(GameInstallation installation, LaunchProfile profile)
    {
        try { Prepare(installation, profile); return true; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { return false; }
    }
    // Windows argv quoting, for an accurate preview only. Execution uses ArgumentList, never a shell.
    public static string QuoteArgument(string value)
    {
        var result = new StringBuilder("\""); int slashes = 0;
        foreach (var c in value)
        {
            if (c == '\\') { slashes++; continue; }
            result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes); slashes = 0;
            result.Append(c);
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }
}
