using ArcHive.Core;
using System.Net;
using System.Text.Json;
internal static class DownloadTests
{
    public static async Task Run(string root,Action<bool,string> check)
    {
        void Reject(string url,string name){try{DownloadQueue.Validate(url,name);check(false,"Unsafe download accepted");}catch(InvalidDataException){check(true,"Unsafe download rejected");}}
        Reject("https://example.com/game.zip","CON.zip");Reject("http://example.com/game.zip","game.zip");Reject("https://example.com/game.zip?token=secret","game.zip");Reject("https://example.com/a","../game.zip");Reject("https://localhost/a","game.zip");Reject("https://example.com/a","game.exe");Reject("https://user:pass@example.com/a","game.zip");
        check(!DownloadQueue.PublicAddress(IPAddress.Parse("127.0.0.1"))&&!DownloadQueue.PublicAddress(IPAddress.Parse("10.0.0.1"))&&!DownloadQueue.PublicAddress(IPAddress.Parse("169.254.169.254"))&&DownloadQueue.PublicAddress(IPAddress.Parse("8.8.8.8")),"Download transport rejects private addresses");
        using(var bytes=new MemoryStream()){
            using(var zip=new System.IO.Compression.ZipArchive(bytes,System.IO.Compression.ZipArchiveMode.Create,true))using(var writer=new StreamWriter(zip.CreateEntry("sample/readme.txt").Open()))writer.Write("Downloaded fixture");
            var area=Path.Combine(root,"download-extract");
            using var downloads=new DownloadQueue(Path.Combine(area,"downloads"));var item=downloads.Add("https://example.com/sample.zip","sample.zip");
            using var client=new HttpClient(new Stub(false,bytes.ToArray()));await downloads.RunAsync(client,null,default);var archivePath=await downloads.ReviewAsync(item);
            check((await ArchiveInspection.ReadAsync(archivePath)).CanExtract,"Downloaded ZIP passes extraction preview");
            var game=new Game{Title="Sample",Archives=[new(){Path=archivePath}]};var library=new Library{Games=[game]};
            var store=new SqliteLibraryStore(Path.Combine(area,"library.db"));store.Load();store.Save(library);
            using var extractions=new ExtractionQueue(Path.Combine(area,"extractions.json"));extractions.Enqueue(game,game.Archives[0],Path.Combine(area,"output"));
            await extractions.RunAsync(new MultiArchiveExtractor(),job=>{library=ExtractionQueue.Link(library,job);store.Save(library);return Task.CompletedTask;},null,default);
            var installed=store.Load().Games.Single().Installations.Single();
            check(extractions.Jobs.Single().State==ExtractionState.Completed&&File.ReadAllText(Path.Combine(installed.Path,"sample/readme.txt"))=="Downloaded fixture","Download-review-extract pipeline persists correct installation payload");
            check(installed.SourceArchiveId==game.Archives[0].Id&&File.Exists(archivePath),"Pipeline retains downloaded archive and source association");
        }
        var folder=Path.Combine(root,"download-test");
        using(var q=new DownloadQueue(folder)){
            var job=q.Add("https://example.com/game.zip","game.zip");
            using var client=new HttpClient(new Stub(false));await q.RunAsync(client,null,default);
            check(job.State==DownloadState.Ready&&File.Exists(await q.ReviewAsync(job)),"Successful archive download can be reviewed");
            File.AppendAllText(q.FilePath(job),"changed");try{await q.ReviewAsync(job);check(false,"Changed file accepted");}catch(InvalidDataException){check(true,"Changed download rejected at review");}
            var bad=q.Add("https://example.com/login","bad.zip");using var badClient=new HttpClient(new Stub(true));await q.RunAsync(badClient,null,default);check(bad.State==DownloadState.Failed&&!File.Exists(q.FilePath(bad)),"HTML response never becomes ready archive");
            var pending=q.Add("https://example.com/next.zip","next.zip");using var cancelled=new CancellationTokenSource();cancelled.Cancel();await q.RunAsync(client,null,cancelled.Token);check(pending.State==DownloadState.Pending,"Cancelled queue leaves pending work untouched");
            try{using var second=new DownloadQueue(folder);check(false,"Queue lease bypassed");}catch(IOException){check(true,"Concurrent queue owner blocked");}
        }
        var recoveryFolder=Path.Combine(root,"download-move-recovery");
        using(var q=new DownloadQueue(recoveryFolder)){q.Add("https://example.com/game.zip","game.zip");using var client=new HttpClient(new Stub(false));await q.RunAsync(client,null,default);}
        var recoveryFile=Path.Combine(recoveryFolder,"queue.json");var recovery=JsonSerializer.Deserialize<List<DownloadJob>>(File.ReadAllText(recoveryFile))!;recovery[0].State=DownloadState.Downloading;File.WriteAllText(recoveryFile,JsonSerializer.Serialize(recovery));
        using(var q=new DownloadQueue(recoveryFolder))check(q.Jobs[0].State==DownloadState.Ready&&File.Exists(await q.ReviewAsync(q.Jobs[0])),"Crash after final move recovers verified completed archive");
        var file=Path.Combine(folder,"queue.json");var rows=JsonSerializer.Deserialize<List<DownloadJob>>(File.ReadAllText(file))!;rows[^1].State=DownloadState.Downloading;File.WriteAllText(file,JsonSerializer.Serialize(rows));
        using(var q=new DownloadQueue(folder))check(q.Jobs[^1].State==DownloadState.Interrupted,"Restart recovers active download as interrupted");
    }
    private sealed class Stub(bool html, byte[]? payload=null):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){RequestMessage=request,Content=new ByteArrayContent(payload ?? (html?"<html>login</html>"u8.ToArray():new byte[]{80,75,5,6,0,0,0,0}))});
    }
}
