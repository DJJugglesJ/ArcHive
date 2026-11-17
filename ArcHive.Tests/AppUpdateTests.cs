using ArcHive.Core;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
internal static class AppUpdateTests
{
    public static async Task Run(string root, Action<bool,string> check)
    {
        var bytes=Encoding.UTF8.GetBytes("synthetic installer fixture; never executed");
        var hash=Convert.ToHexString(SHA256.HashData(bytes));
        string Json(string tag="v99.0.0", bool prerelease=false, string? digest=null, string? url=null) => JsonSerializer.Serialize(new {tag_name=tag,draft=false,prerelease,body="Release notes",assets=new[]{new {name=$"ArcHive-{tag[1..]}-win-x64.msi",size=bytes.Length,digest=digest ?? "sha256:"+hash,browser_download_url=url ?? $"{AppUpdates.Repository}/releases/download/{tag}/ArcHive-{tag[1..]}-win-x64.msi"}}});
        var release=AppUpdates.Parse(Encoding.UTF8.GetBytes(Json()));
        check(release.Version==new Version(99,0,0) && release.Sha256==hash.ToLowerInvariant(),"Updater parses stable version and digest");
        async Task Reject(Func<Task> action,string name) { try { await action(); } catch (Exception ex) when(ex is InvalidDataException or InvalidOperationException or OperationCanceledException or IOException) { check(true,name);return; } throw new Exception(name); }
        await Reject(()=>Task.Run(()=>AppUpdates.Parse(Encoding.UTF8.GetBytes(Json(prerelease:true)))),"Updater rejects prereleases");
        await Reject(()=>Task.Run(()=>AppUpdates.Parse(Encoding.UTF8.GetBytes(Json(tag:"v99.0.0-beta")))),"Updater rejects nonstable tags");
        await Reject(()=>Task.Run(()=>AppUpdates.Parse(Encoding.UTF8.GetBytes(Json(digest:"")))),"Updater requires SHA-256 evidence");
        await Reject(()=>Task.Run(()=>AppUpdates.Parse(Encoding.UTF8.GetBytes(Json(url:"https://example.com/installer.msi")))),"Updater refuses foreign release assets");
        using var client=new HttpClient(new Handler(r=>new(HttpStatusCode.OK) {Content=new ByteArrayContent(r.RequestUri!.Host=="api.github.com" ? Encoding.UTF8.GetBytes(Json()) : bytes)}));
        var service=new AppUpdates(client); var found=await service.CheckAsync(default);
        var folder=Path.Combine(root,"updates");
        var path=await service.DownloadAsync(found,folder,null,default);
        check(File.ReadAllBytes(path).SequenceEqual(bytes) && !Directory.EnumerateFiles(folder,"*.partial").Any(),"Verified download is promoted only after size and digest checks");
        var alternate=Path.Combine(root,"chosen-update-location");
        var relocated=await service.DownloadAsync(found,alternate,null,default);
        check(Path.GetDirectoryName(relocated)==alternate && File.ReadAllBytes(relocated).SequenceEqual(bytes),"Update download honors an alternate staging folder and verifies bytes");
        var retry=await service.DownloadAsync(found,alternate,null,default);
        check(retry!=relocated && File.Exists(relocated),"Retry retains previous verified package without overwriting it");
        await Reject(()=>service.DownloadAsync(found,relocated,null,default),"Unusable staging path fails without silently changing location");
        await Reject(()=>service.DownloadAsync(release with {Version=AppUpdates.Current},folder,null,default),"Updater refuses equal or older versions");
        using var corrupt=new HttpClient(new Handler(_=>new(HttpStatusCode.OK){Content=new ByteArrayContent(new byte[bytes.Length])}));
        await Reject(()=>new AppUpdates(corrupt).DownloadAsync(release,folder,null,default),"Corrupt installer digest rejected");
        check(!Directory.EnumerateFiles(folder,"*.partial").Any(),"Failed download leaves no partial installer");
        using var redirect=new HttpClient(new Handler(_=> {var r=new HttpResponseMessage(HttpStatusCode.Redirect);r.Headers.Location=new("https://example.com/payload");return r;}));
        await Reject(()=>new AppUpdates(redirect).DownloadAsync(release,folder,null,default),"Updater rejects redirects outside GitHub asset hosts");
        using var cancel=new CancellationTokenSource();cancel.Cancel();
        await Reject(()=>service.DownloadAsync(release,folder,null,cancel.Token),"Update download honors cancellation");
        await File.WriteAllBytesAsync(path,new byte[bytes.Length]);
        await Reject(()=>AppUpdates.VerifyAsync(path,release,default),"Installer is reverified to catch staged file tampering");
        using var shortClient=new HttpClient(new Handler(_=>new(HttpStatusCode.OK){Content=new ByteArrayContent([1,2])}));
        await Reject(()=>new AppUpdates(shortClient).DownloadAsync(release,folder,null,default),"Updater rejects package length mismatch");
        var prefs=new AppPreferences(); check(!prefs.CheckForUpdatesOnStartup,"Update startup checks are opt-in");
        var settings=new AppPreferencesStore(Path.Combine(root,"update-settings.json")); prefs.CheckForUpdatesOnStartup=true; settings.Save(prefs);
        check(settings.Load().CheckForUpdatesOnStartup,"Automatic update preference survives reload");
        using var oversized=new HttpClient(new Handler(_=>new(HttpStatusCode.OK){Content=new ByteArrayContent(new byte[bytes.Length+1])}));
        await Reject(()=>new AppUpdates(oversized).DownloadAsync(release,folder,null,default),"Updater rejects oversized packages");
    }
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> response):HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) {token.ThrowIfCancellationRequested();return Task.FromResult(response(request));} }
}
