using System.IO.Compression;
using ArcHive.Core;

if (args.Length >= 2 && args[0] == "--launch-probe")
{
    File.WriteAllText(args[1], System.Text.Json.JsonSerializer.Serialize(new { Arguments = args.Skip(2).ToArray(), WorkingDirectory = Environment.CurrentDirectory }));
    return;
}

if (args.Length > 0 && args[0] == "--wait-process") { await Task.Delay(3500); return; }
if (args.Length > 0 && args[0] == "--parent-process")
{
    using var child = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "ArcHive.Tests.exe")) { UseShellExecute = false, CreateNoWindow = true, ArgumentList = { "--wait-process" } });
    await Task.Delay(1500); return;
}
if (File.Exists(Path.Combine(AppContext.BaseDirectory, "plugin-fixture.mode")))
{
    var request = System.Text.Json.JsonSerializer.Deserialize<PluginRequest>(Console.In.ReadToEnd(), PluginCatalog.Json)!;
    if (request.Query?.Title == "wait") await Task.Delay(30000);
    if (request.Query?.Title == "crash") { Environment.Exit(7); return; }
    if (request.Query?.Title == "oversize") { Console.Write(new string('x', 2_100_000)); return; }
    if (request.Query?.Title == "bad") { Console.Write("not JSON"); return; }
    Console.Write(System.Text.Json.JsonSerializer.Serialize(new PluginResponse(1, [], request.Launch), PluginCatalog.Json)); return;
}

if (args.Length == 2 && args[0] is "--itch-live" or "--rawg-live") { await MetadataLiveProbe.Run(args[1],null,args[0] == "--itch-live" ? "itch" : "rawg"); return; }
if (args.Length is 2 or 3 && args[0] == "--providers-live")
{
    await MetadataLiveProbe.Run(args[1], args.Length == 3 ? args[2] : null); return;
}
if (args.Length == 2 && args[0] == "--f95-live")
{
    using var client = new System.Net.Http.HttpClient(new System.Net.Http.HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
    var result = await new F95ThreadSource(client).CheckAsync(args[1], default);
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { result.Candidate.Title, result.Version, TagCount = result.Candidate.Fields.GetValueOrDefault(MetadataField.Tags)?.Split(',').Length ?? 0, DescriptionLength = result.Candidate.Fields.GetValueOrDefault(MetadataField.Description)?.Length ?? 0 })); return;
}
if (args.Length == 2 && args[0] == "--metadata-probe")
{
    var result = F95ThreadSource.Parse("https://f95zone.to/threads/65211/", File.ReadAllText(args[1]));
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { result.Candidate.Title, result.Version, Release = result.Candidate.ObservedRelease, TagCount = result.Candidate.Fields.GetValueOrDefault(MetadataField.Tags)?.Split(',').Length ?? 0, DescriptionLength = result.Candidate.Fields.GetValueOrDefault(MetadataField.Description)?.Length ?? 0, Engine = result.Candidate.Fields.GetValueOrDefault(MetadataField.Engine) })); return;
}
var root = Path.Combine(Path.GetTempPath(), "archive-test-" + Guid.NewGuid());
Directory.CreateDirectory(root);
var passed = 0;
void Check(bool result, string name) { if (!result) throw new Exception(name); Console.WriteLine("PASS " + name); passed++; }
string MakeZip(string name, params (string Name, string Content)[] files)
{
    var path = Path.Combine(root, name + ".zip");
    using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
    foreach (var file in files) { using var writer = new StreamWriter(zip.CreateEntry(file.Name).Open()); writer.Write(file.Content); }
    return path;
}
async Task Reject(string zip, ZipExtractor? extractor = null)
{
    var parent = Path.Combine(root, Guid.NewGuid().ToString());
    try { await (extractor ?? new ZipExtractor()).ExtractAsync(zip, parent, null, default); throw new Exception("Unsafe archive accepted"); }
    catch (InvalidDataException) { Check(!Directory.EnumerateFileSystemEntries(parent).Any(), "Rejected and cleaned " + Path.GetFileName(zip)); }
}
try
{
    var parsed = FilenameParser.Parse("MyAwesomeGame_v0.8.2b_[Win64].7z");
    Check(parsed.Title == "My Awesome Game" && parsed.Version == "0.8.2b" && parsed.Platform == "Windows x64", "CamelCase release and platform parsing");
    Check(parsed.Original == "MyAwesomeGame_v0.8.2b_[Win64].7z", "Original filename preserved");
    Check(FilenameParser.Parse("DOOM_2.zip").Title == "DOOM 2", "Numeric title preserved");
    Check(FilenameParser.Parse("XMLQuest_linux.zip").Title == "XML Quest", "Acronym boundaries");
    Check(FilenameParser.Parse("WinterGame.zip").Title == "Winter Game" && FilenameParser.Parse("WinterGame.zip").Platform is null, "Platform requires token boundary");
    var normal = MakeZip("normal", ("Game/game.exe", "test fixture"), ("Game/data.txt", "data"));
    var parent = Path.Combine(root, "extracted");
    var extracted = await new ZipExtractor().ExtractAsync(normal, parent, null, default);
    Check(File.ReadAllText(Path.Combine(extracted, "Game", "data.txt")) == "data", "Nested ZIP extraction");
    Check(File.Exists(normal), "Archive preserved");
    var second = await new ZipExtractor().ExtractAsync(normal, parent, null, default);
    Check(second != extracted && Directory.Exists(extracted), "No overwrite on repeated extraction");
    await Reject(MakeZip("traversal", ("../escape.txt", "bad")));
    await Reject(MakeZip("absolute", ("C:/escape.txt", "bad")));
    await Reject(MakeZip("stream", ("file:stream", "bad")));
    await Reject(MakeZip("reserved", ("CON.txt", "bad")));
    await Reject(MakeZip("duplicate", ("A.txt", "one"), ("a.txt", "two")));
    await Reject(normal, new ZipExtractor(4));
    await Reject(normal, new ZipExtractor(entryLimit: 1));
    var link = MakeZip("link", ("shortcut", "../outside"));
    using (var z = ZipFile.Open(link, ZipArchiveMode.Update)) z.Entries[0].ExternalAttributes = unchecked((int)0xA1FF0000);
    await Reject(link);
    using var cancel = new CancellationTokenSource(); cancel.Cancel();
    try { await new ZipExtractor().ExtractAsync(normal, parent, null, cancel.Token); throw new Exception("Cancellation ignored"); }
    catch (OperationCanceledException) { Check(true, "Cancellation"); }
    using var during = new CancellationTokenSource();
    var cancelledParent = Path.Combine(root, "cancelled");
    try
    {
        await new ZipExtractor().ExtractAsync(normal, cancelledParent, new CancelProgress(during), during.Token);
        throw new Exception("Cancellation during extraction ignored");
    }
    catch (OperationCanceledException)
    { Check(!Directory.EnumerateFileSystemEntries(cancelledParent).Any(), "In-flight cancellation cleans staging"); }
    File.WriteAllText(Path.Combine(root, "unpacked.rar"), "index fixture only");
    File.WriteAllText(Path.Combine(root, "unpacked.7z"), "index fixture only");
    var library = new Library();
    var result = Scanner.Scan(root, default); Scanner.Merge(library, result.Games);
    Check(library.Games.Any(g => g.Archives.Any(a => a.Path.EndsWith(".rar"))) && library.Games.Any(g => g.Archives.Any(a => a.Path.EndsWith(".7z"))), "RAR and 7z discovery");
    var count = library.Games.Count; library.Games[0].Favorite = true; library.Games[0].Description = "Keep me";
    Scanner.Merge(library, Scanner.Scan(root, default).Games);
    Check(library.Games.Count == count && library.Games[0].Favorite && library.Games[0].Description == "Keep me", "Idempotent scan preserves metadata");
    var store = new LibraryStore(Path.Combine(root, "library.json")); store.Save(library); store.Save(library);
    Check(store.Load().Games.Count == count && File.Exists(Path.Combine(root, "library.json.bak")), "Persistence and backup");
    Check(!PathSafety.IsWithin(Path.Combine(root, "game"), Path.Combine(root, "game-other", "a.exe")), "Sibling prefix containment");
    var missing = new Game { ArchivePath = Path.Combine(root, "nonexistent.zip") };
    Check(missing.State == "Missing", "Missing archive state");
    File.WriteAllText(Path.Combine(root, "library.json"), "invalid json");
    try { store.Load(); throw new Exception("Corrupt data accepted"); }
    catch (System.Text.Json.JsonException) { Check(File.ReadAllText(Path.Combine(root, "library.json")) == "invalid json", "Corrupt library is not overwritten"); }
    await ArchiveTests.Run(root, Check);
    SqliteTests.Run(root, Check);
    MilestoneTests.Run(root, Check);
    await MetadataTests.Run(root, Check);
    await ProviderTests.Run(Check);
    await ArtworkGamepadTests.Run(root, Check);
    PreferenceTests.Run(root, Check);
    NativeControllerTests.Run(Check);
    ControllerWorkflowTests.Run(Check);
    await OrganizationTests.Run(root, Check);
    await ExtractionQueueTests.Run(root, Check);
    await DetectionTests.Run(root, Check);
    await SaveBackupTests.Run(root, Check);
    await SourceWorkflowTests.Run(root, Check);
    await V1Tests.Run(root, Check);
    await MetadataScrubbingTests.Run(root, Check);
    Console.WriteLine($"All {passed} checks passed.");
}
finally { Directory.Delete(root, true); }

sealed class CancelProgress(CancellationTokenSource source) : IProgress<string>
{ public void Report(string value) => source.Cancel(); }
