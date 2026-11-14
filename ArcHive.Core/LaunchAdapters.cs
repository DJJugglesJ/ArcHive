using System.Globalization;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Xml;
using System.Xml.Linq;

namespace ArcHive.Core;

public interface ILaunchAdapter
{
    string Id { get; }
    Task<LaunchPlan> PrepareAsync(LaunchPlan native, LaunchProfile profile, CancellationToken cancellationToken);
}
public sealed record LocaleProfile(string Guid, string Name, string Locale, string TimeZone, int CodePage, string Parameters, bool Elevated, bool Suspended)
{
    public string AdapterId => "locale-emulator/" + Guid;
    public override string ToString() => $"{Name} · {Locale} · CP{CodePage} · {TimeZone}";
}
public sealed class LocaleEmulatorAdapter(string executable) : ILaunchAdapter
{
    public string Id => "locale-emulator";
    public static string ValidateInstallation(string executable)
    {
        if (!OperatingSystem.IsWindows() || RuntimeInformation.OSArchitecture is not (Architecture.X64 or Architecture.X86))
            throw new PlatformNotSupportedException("Locale Emulator requires x86 or x64 Windows. ARM is not supported.");
        if (!Path.IsPathFullyQualified(executable) || !Path.GetFileName(executable).Equals("LEProc.exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Choose LEProc.exe from your Locale Emulator installation.");
        PathSafety.NoLinks(executable);
        var folder = Path.GetDirectoryName(executable)!;
        foreach (var name in new[] { "LEProc.exe", "LoaderDll.dll", "LocaleEmulator.dll", "LEConfig.xml" })
        {
            var file = Path.Combine(folder, name); PathSafety.NoLinks(file);
            if (!File.Exists(file)) throw new FileNotFoundException($"Locale Emulator file couldn't be found: {name}. Choose a complete installation.");
        }
        return folder;
    }
    public static IReadOnlyList<LocaleProfile> ReadProfiles(string executable)
    {
        var file = Path.Combine(ValidateInstallation(executable), "LEConfig.xml");
        if (new FileInfo(file).Length > 1_000_000) throw new InvalidDataException("Locale Emulator configuration exceeds 1 MB.");
        using var reader = XmlReader.Create(file, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1_000_000 });
        var document = XDocument.Load(reader);
        var elements = document.Root?.Element("Profiles")?.Elements("Profile").ToArray() ?? [];
        if (elements.Length > 256) throw new InvalidDataException("Too many Locale Emulator profiles.");
        var results = new List<LocaleProfile>();
        foreach (var item in elements)
        {
            string Text(string name) => item.Element(name)?.Value ?? "";
            var id = item.Attribute("Guid")?.Value ?? "";
            if (!System.Guid.TryParse(id, out _)) throw new InvalidDataException("Invalid Locale Emulator profile ID.");
            var locale = CultureInfo.GetCultureInfo(Text("Location"));
            var zone = Text("Timezone"); TimeZoneInfo.FindSystemTimeZoneById(zone);
            var name = item.Attribute("Name")?.Value ?? locale.Name;
            if (name.Length > 200 || Text("Parameter").Length > 8192) throw new InvalidDataException("Oversized Locale Emulator profile.");
            results.Add(new(id, name, locale.Name, zone, locale.TextInfo.ANSICodePage, Text("Parameter"), Text("RunAsAdmin").Equals("true", StringComparison.OrdinalIgnoreCase), Text("RunWithSuspend").Equals("true", StringComparison.OrdinalIgnoreCase)));
        }
        if (results.Select(p => p.Guid).Distinct(StringComparer.OrdinalIgnoreCase).Count() != results.Count) throw new InvalidDataException("Duplicate Locale Emulator profile IDs.");
        return results;
    }
    public static bool IsSupportedTarget(string executable)
    {
        try
        {
            using var stream = File.OpenRead(executable); using var pe = new PEReader(stream);
            if (pe.PEHeaders.CoffHeader.Machine != Machine.I386 || pe.PEHeaders.PEHeader?.Magic != PEMagic.PE32) return false;
            var cor = pe.PEHeaders.CorHeader;
            return cor is null || (cor.Flags & (CorFlags.Requires32Bit | CorFlags.Prefers32Bit)) != 0;
        }
        catch (BadImageFormatException) { return false; }
    }
    public Task<LaunchPlan> PrepareAsync(LaunchPlan native, LaunchProfile profile, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var selected = ReadProfiles(executable).SingleOrDefault(p => p.AdapterId == profile.AdapterId)
            ?? throw new InvalidDataException("The Locale Emulator profile is unavailable. Choose a profile in Launch.");
        if (!IsSupportedTarget(native.Executable)) throw new NotSupportedException("Locale Emulator supports 32-bit Windows executables. This executable is 64-bit, AnyCPU, or unrecognized.");
        if (!Game.SamePath(native.WorkingDirectory, Path.GetDirectoryName(native.Executable)!))
            throw new NotSupportedException("Locale Emulator uses the executable folder as its working directory. Clear the custom Working Directory first.");
        if (selected.Parameters.Length > 0 && native.Arguments.Count > 0)
            throw new InvalidDataException("This Locale Emulator profile defines its own arguments. Clear one argument source to avoid silently overriding the other.");
        if (selected.Suspended) throw new NotSupportedException("This Locale Emulator profile starts suspended. Choose a profile without debug suspension.");
        // LEProc rejoins parsed argv without quoting; preserve each game's argument through that extra layer.
        var arguments = new[] { "-runas", selected.Guid, native.Executable }.Concat(native.Arguments.Select(LaunchService.QuoteArgument)).ToArray();
        return Task.FromResult(new LaunchPlan(Path.GetFullPath(executable), native.WorkingDirectory, arguments));
    }
    public static IReadOnlyList<string> Detect(IEnumerable<string> directories)
    {
        var results = new List<string>();
        foreach (var folder in directories.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try { var path = Path.Combine(folder, "LEProc.exe"); if (File.Exists(path)) { ReadProfiles(path); results.Add(path); } }
            catch (Exception ex) when (ex is IOException or ArgumentException or XmlException or InvalidDataException or UnauthorizedAccessException or TimeZoneNotFoundException or NotSupportedException) { }
        }
        return results;
    }
}
