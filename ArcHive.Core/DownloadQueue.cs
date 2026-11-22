using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Security.Cryptography;
namespace ArcHive.Core;

public enum DownloadState { Pending, Downloading, Interrupted, Ready, Failed, Cancelled }
public sealed class DownloadJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Url { get; set; } = "";
    public string Name { get; set; } = "";
    public DownloadState State { get; set; }
    public string Hash { get; set; } = "";
    public long Bytes { get; set; }
    public override string ToString() => $"{Name} · {State} · {Bytes / 1048576d:0.0} MB";
}
// Public, permanent links only. Never persist authentication headers, cookies or signed query tokens.
public sealed class DownloadQueue : IDisposable
{
    public const long MaximumBytes = 32L * 1024 * 1024 * 1024;
    private readonly string folder, journal;
    private readonly FileStream lease;
    public List<DownloadJob> Jobs { get; private set; }
    public bool Running { get; private set; }
    public DownloadQueue(string folder)
    {
        this.folder=Path.GetFullPath(folder); PathSafety.NoLinks(this.folder); Directory.CreateDirectory(this.folder);
        journal=Path.Combine(this.folder,"queue.json"); PathSafety.NoLinks(journal); PathSafety.NoLinks(journal+".lock");
        lease=new FileStream(journal+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        try {
            if(File.Exists(journal)&&new FileInfo(journal).Length>2_000_000)throw new InvalidDataException("Download queue is too large.");
            Jobs=File.Exists(journal)?JsonSerializer.Deserialize<List<DownloadJob>>(File.ReadAllText(journal))??throw new InvalidDataException():[];
            if(Jobs.Count>1000||Jobs.Select(j=>j.Id).Distinct().Count()!=Jobs.Count)throw new InvalidDataException();
            foreach(var j in Jobs){Validate(j.Url,j.Name);if(!Enum.IsDefined(j.State))throw new InvalidDataException();if(j.State==DownloadState.Downloading){
                    j.State=DownloadState.Interrupted;
                    var completed=FilePath(j);PathSafety.NoLinks(completed);
                    if(File.Exists(completed)&&j.Hash.Length==64){using var stream=File.OpenRead(completed);if(stream.Length==j.Bytes&&Convert.ToHexString(SHA256.HashData(stream))==j.Hash)j.State=DownloadState.Ready;}
                }}
            Save();
        } catch {lease.Dispose();throw;}
    }
    public static Uri Validate(string url,string name)
    {
        if(url.Length>4096||!Uri.TryCreate(url,UriKind.Absolute,out var u)||u.Scheme!="https"||!u.IsDefaultPort||u.UserInfo.Length>0||u.Query.Length>0||u.Fragment.Length>0||u.IsLoopback||!u.Host.Contains('.'))
            throw new InvalidDataException("Use a public HTTPS file link without login details, query tokens or fragments. Download other links in your browser.");
        if(name.Length is <1 or >180||name!=Path.GetFileName(name)||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0||System.Text.RegularExpressions.Regex.IsMatch(name,@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])\.",System.Text.RegularExpressions.RegexOptions.IgnoreCase)||!new[]{".zip",".rar",".7z"}.Contains(Path.GetExtension(name).ToLowerInvariant()))
            throw new InvalidDataException("Choose a ZIP, RAR or 7z filename without a folder path.");
        return u;
    }
    public DownloadJob Add(string url,string name)
    {
        if(Running||Jobs.Count>=1000)throw new InvalidOperationException("Stop the queue before adding downloads.");
        Validate(url,name);var j=new DownloadJob{Url=url,Name=name};Jobs.Add(j);Save();return j;
    }
    public string FilePath(DownloadJob j)=>Path.Combine(folder,j.Id.ToString("N"),j.Name);
    private void Save()
    {
        PathSafety.NoLinks(folder);var tmp=journal+".tmp";PathSafety.NoLinks(tmp);
        using(var f=new FileStream(tmp,FileMode.Create,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(f,Jobs);f.Flush(true);}
        File.Move(tmp,journal,true);
    }
    public static bool PublicAddress(IPAddress ip)
    {
        if(ip.IsIPv4MappedToIPv6)ip=ip.MapToIPv4();var b=ip.GetAddressBytes();
        if(ip.AddressFamily!=AddressFamily.InterNetwork)return false; // conservative public IPv4 transport
        return b[0] is not (0 or 10 or 127) && b[0]<224 && !(b[0]==169&&b[1]==254) && !(b[0]==172&&b[1]>=16&&b[1]<=31) && !(b[0]==192&&b[1]==168) && !(b[0]==100&&b[1]>=64&&b[1]<=127);
    }
    public static HttpClient CreateClient()
    {
        var handler=new SocketsHttpHandler{AllowAutoRedirect=false,UseCookies=false,UseProxy=false,ConnectCallback=async (ctx,ct)=>{
            var addresses=await Dns.GetHostAddressesAsync(ctx.DnsEndPoint.Host,ct);var ip=addresses.FirstOrDefault(PublicAddress)??throw new IOException("No public download address.");
            var socket=new Socket(ip.AddressFamily,SocketType.Stream,ProtocolType.Tcp);
            try {await socket.ConnectAsync(new IPEndPoint(ip,ctx.DnsEndPoint.Port),ct);return new NetworkStream(socket,true);}catch{socket.Dispose();throw;}
        }};
        var client=new HttpClient(handler){Timeout=Timeout.InfiniteTimeSpan};client.DefaultRequestHeaders.UserAgent.ParseAdd("ArcHive/1.0");return client;
    }
    public async Task RunAsync(HttpClient client,IProgress<long>? progress,CancellationToken token)
    {
        if(Running)throw new InvalidOperationException("Downloads are already running.");Running=true;
        try {foreach(var j in Jobs.Where(j=>j.State is DownloadState.Pending or DownloadState.Interrupted or DownloadState.Failed or DownloadState.Cancelled)){
            if(token.IsCancellationRequested)break;
            j.State=DownloadState.Downloading;j.Bytes=0;j.Hash="";Save();
            var target=FilePath(j);var partial=target+".partial";
            try {
                PathSafety.NoLinks(target);PathSafety.NoLinks(partial);Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromHours(6));
                var uri=Validate(j.Url,j.Name);
                for(var hop=0;;hop++){
                    using var response=await client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
                    if(response.RequestMessage?.RequestUri!=uri)throw new InvalidDataException("Automatic redirects are not supported.");
                    if((int)response.StatusCode is >=300 and <400){if(hop>=5||response.Headers.Location is null)throw new IOException();uri=Validate(new Uri(uri,response.Headers.Location).AbsoluteUri,j.Name);continue;}
                    response.EnsureSuccessStatusCode();
                    if(response.Content.Headers.ContentLength is >MaximumBytes)throw new IOException("Download exceeds limit.");
                    await using(var input=await response.Content.ReadAsStreamAsync(timeout.Token))
                    await using(var output=new FileStream(partial,FileMode.Create,FileAccess.Write,FileShare.None,81920,true)){
                        var lastProgress=Environment.TickCount64; var buffer=new byte[81920];int n;while((n=await input.ReadAsync(buffer,timeout.Token))>0){j.Bytes+=n;if(j.Bytes>MaximumBytes)throw new IOException();await output.WriteAsync(buffer.AsMemory(0,n),timeout.Token);if(Environment.TickCount64-lastProgress>=200){progress?.Report(j.Bytes);lastProgress=Environment.TickCount64;}}
                    }
                    if(response.Content.Headers.ContentLength is long expected&&expected!=j.Bytes)throw new IOException("Incomplete download.");
                    break;
                }
                await using(var f=File.OpenRead(partial)){
                    var magic=new byte[8];var n=await f.ReadAsync(magic,timeout.Token);
                    var ext=Path.GetExtension(j.Name).ToLowerInvariant();
                    var valid=ext switch {".zip"=>n>=4&&magic[0]==80&&magic[1]==75&&((magic[2]==3&&magic[3]==4)||(magic[2]==5&&magic[3]==6)),".rar"=>n>=7&&magic.AsSpan(0,6).SequenceEqual(new byte[]{82,97,114,33,26,7}),".7z"=>n>=6&&magic.AsSpan(0,6).SequenceEqual(new byte[]{55,122,188,175,39,28}),_=>false};
                    if(!valid)throw new InvalidDataException("Response is not the selected archive format.");
                    f.Position=0;j.Hash=Convert.ToHexString(await SHA256.HashDataAsync(f,timeout.Token));
                }
                Save(); // Persist hash before publishing, allowing recovery after the move.
                File.Move(partial,target,false);j.State=DownloadState.Ready;
            }catch(OperationCanceledException){j.State=token.IsCancellationRequested?DownloadState.Cancelled:DownloadState.Failed;}
            catch{j.State=DownloadState.Failed;}
            finally{Save();}
        }}finally{Running=false;}
    }
    public async Task<string> ReviewAsync(DownloadJob j, CancellationToken token = default)
    {
        if(j.State!=DownloadState.Ready)throw new InvalidOperationException("Download must finish before review.");
        var path=FilePath(j);PathSafety.NoLinks(path);await using var f=File.OpenRead(path);
        if(f.Length!=j.Bytes||Convert.ToHexString(await SHA256.HashDataAsync(f,token))!=j.Hash)throw new InvalidDataException("The downloaded file changed. Download a fresh copy.");return path;
    }
    public void Dispose()=>lease.Dispose();
}

