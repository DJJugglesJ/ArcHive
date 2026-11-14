using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ArcHive.Core;

public sealed record PluginManifest(int ApiVersion, string Id, string Name, string Version, string MinimumAppVersion, string Executable, string[] Capabilities);
public sealed record InstalledPlugin(PluginManifest Manifest, string Folder, Dictionary<string, string> Hashes, bool Enabled)
{
    public override string ToString() => $"{Manifest.Name} {Manifest.Version} · {string.Join(", ", Manifest.Capabilities)} · {(Enabled ? "Enabled" : "Disabled")}";
}
public sealed record PluginRequest(int ApiVersion, string Operation, MetadataQuery? Query = null, LaunchPlan? Launch = null);
public sealed record PluginResponse(int ApiVersion, MetadataCandidate[]? Candidates = null, LaunchPlan? Plan = null);

// Explicitly installed extensions only. No discovery in game folders and no in-process assembly loading.
public sealed class PluginCatalog(string dataDirectory)
{
    public static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() }, WriteIndented = true };
    private string Root => Path.Combine(dataDirectory, "plugins");
    private string Registry => Path.Combine(dataDirectory, "plugins.json");
    public IReadOnlyList<InstalledPlugin> List()
    {
        if (!File.Exists(Registry)) return [];
        PathSafety.NoLinks(Registry);
        if (new FileInfo(Registry).Length > 4_000_000) throw new InvalidDataException("Plugin registry exceeds its size limit.");
        var plugins = JsonSerializer.Deserialize<List<InstalledPlugin>>(File.ReadAllText(Registry), Json) ?? throw new InvalidDataException("Invalid plugin registry.");
        if (plugins.Count > 32 || plugins.Any(p => p is null || p.Manifest is null) || plugins.Select(p => p.Manifest.Id).Distinct().Count() != plugins.Count) throw new InvalidDataException("Invalid plugin registry.");
        foreach (var plugin in plugins) { ValidateManifest(plugin.Manifest); CheckedFolder(plugin); }
        return plugins;
    }
    public static void ValidateManifest(PluginManifest manifest)
    {
        if (manifest is null || manifest.ApiVersion != 1 || manifest.Id is null || !Regex.IsMatch(manifest.Id, "^[a-z][a-z0-9.-]{2,59}$", RegexOptions.CultureInvariant) ||
            string.IsNullOrWhiteSpace(manifest.Name) || manifest.Name.Length > 100 || !Version.TryParse(manifest.Version, out _) ||
            !Version.TryParse(manifest.MinimumAppVersion, out var minimum) || minimum > typeof(PluginCatalog).Assembly.GetName().Version ||
            manifest.Capabilities is null || manifest.Capabilities.Length is < 1 or > 2 || manifest.Capabilities.Distinct().Count() != manifest.Capabilities.Length ||
            manifest.Capabilities.Any(c => c is not ("metadata" or "launch")) || string.IsNullOrWhiteSpace(manifest.Executable) ||
            !manifest.Executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The plugin manifest is invalid, incompatible, or requests unsupported capabilities.");
        RelativeFile("C:\\ArcHivePlugin", manifest.Executable);
    }
    private static string RelativeFile(string root, string relative)
    {
        if (Path.IsPathRooted(relative) || relative.Contains(':') || relative.Split('/', '\\').Any(p => p is ".." or "." or "")) throw new InvalidDataException("Plugin file paths must stay inside the plugin folder.");
        var path = Path.GetFullPath(relative, Path.GetFullPath(root));
        if (!PathSafety.IsWithin(root, path)) throw new InvalidDataException("Plugin path escapes its folder.");
        return path;
    }
    private string CheckedFolder(InstalledPlugin plugin)
    {
        if (!Guid.TryParseExact(plugin.Folder, "N", out _)) throw new InvalidDataException("Invalid managed plugin folder.");
        var folder = Path.Combine(Root, plugin.Folder); PathSafety.NoLinks(folder); return folder;
    }
    private static Dictionary<string, string> HashFiles(string root)
    {
        PathSafety.NoLinks(root); var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); long bytes = 0;
        var pending = new Stack<string>(); pending.Push(root); var directoryCount = 0;
        while (pending.TryPop(out var directory))
        {
            if (++directoryCount > 512) throw new InvalidDataException("Plugin contains too many folders.");
            foreach (var path in Directory.EnumerateFileSystemEntries(directory))
            {
                PathSafety.NoLinks(path);
                if (Directory.Exists(path)) { pending.Push(path); continue; }
                var file = new FileInfo(path); bytes += file.Length;
                if (result.Count >= 1024 || bytes > 256L * 1024 * 1024) throw new InvalidDataException("Plugin exceeds 1,024 files or 256 MB.");
                using var stream = file.OpenRead(); result.Add(Path.GetRelativePath(root, path), Convert.ToHexString(SHA256.HashData(stream)));
            }
        }
        return result;
    }
    private void Save(IEnumerable<InstalledPlugin> plugins)
    {
        Directory.CreateDirectory(dataDirectory); PathSafety.NoLinks(dataDirectory); PathSafety.NoLinks(Registry);
        var temporary = Registry + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(plugins, Json)); File.Move(temporary, Registry, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public InstalledPlugin Install(string source)
    {
        var plugins = List().ToList(); if (plugins.Count >= 32) throw new InvalidDataException("Remove a plugin before adding another (limit 32).");
        source = Path.GetFullPath(source); PathSafety.NoLinks(source);
        if (Game.SamePath(source, Root) || PathSafety.IsWithin(source, Root) || PathSafety.IsWithin(Root, source)) throw new InvalidDataException("Choose a plugin distribution outside ArcHive's managed plugin folder.");
        var file = Path.Combine(source, "plugin.json"); PathSafety.NoLinks(file);
        if (new FileInfo(file).Length > 65536) throw new InvalidDataException("Plugin manifest exceeds 64 KB.");
        var manifest = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(file), Json)!; ValidateManifest(manifest);
        if (plugins.Any(p => p.Manifest.Id == manifest.Id)) throw new InvalidDataException("This plugin is already installed. Remove it before installing a replacement.");
        var hashes = HashFiles(source);
        if (!hashes.ContainsKey(manifest.Executable.Replace('/', Path.DirectorySeparatorChar))) throw new FileNotFoundException("The plugin executable couldn't be found.");
        Directory.CreateDirectory(Root); PathSafety.NoLinks(Root);
        var plugin = new InstalledPlugin(manifest, Guid.NewGuid().ToString("N"), hashes, false); var destination = CheckedFolder(plugin);
        Directory.CreateDirectory(destination);
        try
        {
            foreach (var relative in hashes.Keys)
            {
                var target = RelativeFile(destination, relative); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(RelativeFile(source, relative), target);
            }
            ValidateFiles(plugin); plugins.Add(plugin); Save(plugins); return plugin;
        }
        catch { Directory.Delete(destination, true); throw; }
    }
    public void SetEnabled(string id, bool enabled)
    {
        var plugins = List().ToList(); var index = plugins.FindIndex(p => p.Manifest.Id == id);
        if (index < 0) throw new InvalidDataException("Plugin is no longer installed.");
        if (enabled) ValidateFiles(plugins[index]);
        plugins[index] = plugins[index] with { Enabled = enabled }; Save(plugins);
    }
    public void Remove(string id)
    {
        var plugins = List().ToList(); var plugin = plugins.Single(p => p.Manifest.Id == id); var folder = CheckedFolder(plugin);
        // Unregister first: a partial cleanup must never leave an enabled extension.
        Save(plugins.Where(p => p.Manifest.Id != id));
        if (Directory.Exists(folder)) { HashFiles(folder); Directory.Delete(folder, true); }
    }
    public string ValidateFiles(InstalledPlugin plugin)
    {
        ValidateManifest(plugin.Manifest); var folder = CheckedFolder(plugin); var actual = HashFiles(folder);
        if (plugin.Hashes is null || actual.Count != plugin.Hashes.Count || actual.Any(p => !plugin.Hashes.TryGetValue(p.Key, out var hash) || hash != p.Value))
            throw new InvalidDataException("Plugin files changed after installation. Remove and reinstall the plugin before enabling it.");
        return RelativeFile(folder, plugin.Manifest.Executable);
    }
    public InstalledPlugin Resolve(string id, string capability)
    {
        var plugin = List().SingleOrDefault(p => p.Manifest.Id == id && p.Enabled) ?? throw new InvalidDataException("Plugin is missing or disabled. Check Settings → Plugins.");
        if (!plugin.Manifest.Capabilities.Contains(capability)) throw new InvalidDataException("Plugin does not declare the requested capability.");
        ValidateFiles(plugin); return plugin;
    }
}

public sealed class PluginHost(PluginCatalog catalog, TimeSpan? timeout = null)
{
    public async Task<PluginResponse> InvokeAsync(string id, PluginRequest request, CancellationToken token)
    {
        var capability = request.Operation switch { "metadata.search" => "metadata", "launch.prepare" => "launch", _ => throw new InvalidDataException("Unsupported plugin operation.") };
        token.ThrowIfCancellationRequested(); var plugin = catalog.Resolve(id, capability); var executable = catalog.ValidateFiles(plugin);
        var input = JsonSerializer.Serialize(request, PluginCatalog.Json);
        if (input.Length > 2_000_000) throw new InvalidDataException("Plugin request exceeds 2 MB.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(timeout ?? TimeSpan.FromSeconds(15));
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(executable)!, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.Environment.Clear();
        foreach (var key in new[] { "SystemRoot", "WINDIR", "TEMP", "TMP" }) if (Environment.GetEnvironmentVariable(key) is { } value) info.Environment[key] = value;
        using var process = new Process { StartInfo = info };
        try
        {
            if (!process.Start()) throw new IOException("The plugin process could not start.");
            using var registration = deadline.Token.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { } });
            var output = ReadBounded(process.StandardOutput, 2_000_000, deadline.Token);
            var error = ReadBounded(process.StandardError, 16384, deadline.Token);
            await process.StandardInput.WriteAsync(input.AsMemory(), deadline.Token); process.StandardInput.Close();
            await Task.WhenAll(output, error, process.WaitForExitAsync(deadline.Token)).WaitAsync(deadline.Token);
            if (process.ExitCode != 0) throw new InvalidDataException("Plugin failed. No library changes were made.");
            var response = JsonSerializer.Deserialize<PluginResponse>(await output, PluginCatalog.Json) ?? throw new InvalidDataException("Plugin returned an empty response.");
            if (response.ApiVersion != 1) throw new InvalidDataException("Plugin response uses an unsupported API version.");
            return response;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new TimeoutException("Plugin timed out. No library changes were made."); }
        finally { try { if (!process.HasExited) process.Kill(true); } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { } }
    }
    private static async Task<string> ReadBounded(StreamReader reader, int limit, CancellationToken token)
    {
        var result = new StringBuilder(); var buffer = new char[4096];
        while (true) { var count = await reader.ReadAsync(buffer.AsMemory(), token); if (count == 0) return result.ToString(); if (result.Length + count > limit) throw new InvalidDataException("Plugin output exceeded its size limit."); result.Append(buffer, 0, count); }
    }
}
public sealed class PluginMetadataProvider(PluginCatalog catalog, string pluginId) : IMetadataProvider
{
    public string Id => "plugin:" + pluginId;
    public async Task<IReadOnlyList<MetadataCandidate>> SearchAsync(MetadataQuery query, CancellationToken cancellationToken)
    {
        var response = await new PluginHost(catalog).InvokeAsync(pluginId, new(1, "metadata.search", Query: query), cancellationToken);
        var candidates = response.Candidates ?? throw new InvalidDataException("Plugin returned no candidate array.");
        if (candidates.Length > 500) throw new InvalidDataException("Plugin returned too many candidates.");
        foreach (var candidate in candidates) { MetadataValidation.Validate(candidate); if (candidate.Provider != Id) throw new InvalidDataException("Plugin candidate uses another provider's identity."); }
        return candidates;
    }
}
public sealed class PluginLaunchAdapter(PluginCatalog catalog, string pluginId) : ILaunchAdapter
{
    public string Id => "plugin:" + pluginId;
    public async Task<LaunchPlan> PrepareAsync(LaunchPlan native, LaunchProfile profile, CancellationToken cancellationToken)
    {
        var response = await new PluginHost(catalog).InvokeAsync(pluginId, new(1, "launch.prepare", Launch: native), cancellationToken);
        var plan = response.Plan ?? throw new InvalidDataException("Plugin returned no launch plan.");
        var plugin = catalog.Resolve(pluginId, "launch"); var entry = catalog.ValidateFiles(plugin); var folder = Path.GetDirectoryName(entry)!;
        if (!Game.SamePath(plan.Executable, native.Executable) && (!PathSafety.IsWithin(folder, plan.Executable) || !File.Exists(plan.Executable))) throw new InvalidDataException("Plugin launch executable must be the selected game or an installed plugin executable.");
        if (!Path.IsPathFullyQualified(plan.Executable) || !Path.IsPathFullyQualified(plan.WorkingDirectory)) throw new InvalidDataException("Plugin launch paths must be absolute.");
        PathSafety.NoLinks(plan.Executable);
        if (!plan.Executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !Game.SamePath(plan.WorkingDirectory, native.WorkingDirectory) || plan.Arguments is null || plan.Arguments.Count > 256 || plan.Arguments.Any(a => a is null || a.Length > 8192 || a.Contains('\0')))
            throw new InvalidDataException("Plugin returned an invalid launch plan.");
        return plan;
    }
}
