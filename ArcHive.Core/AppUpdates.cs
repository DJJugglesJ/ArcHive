using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ArcHive.Core;

public sealed record AppRelease(Version Version, string Tag, string Notes, Uri Page, Uri Download, string FileName, long Size, string Sha256);

// Public packaged releases only. No source checkout, credentials, scripts or arbitrary URLs.
public sealed class AppUpdates(HttpClient client)
{
    public const string Repository = "https://github.com/DJJugglesJ/ArcHive";
    public const long MaximumPackage = 512L * 1024 * 1024;
    public static Version Current => new(typeof(AppUpdates).Assembly.GetName().Version!.ToString(3));
    public async Task<AppRelease> CheckAsync(CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(25));
        using var response = await Get(new("https://api.github.com/repos/DJJugglesJ/ArcHive/releases/latest"), false, timeout.Token);
        using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var output = new MemoryStream(); await CopyLimited(input, output, 2_000_000, null, timeout.Token);
        return Parse(output.ToArray());
    }
    public static AppRelease Parse(byte[] json)
    {
        if (json.Length > 2_000_000) throw new InvalidDataException("Release information is too large.");
        using var document = JsonDocument.Parse(json); var root = document.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean() || !Regex.IsMatch(tag, @"^v\d{1,5}\.\d{1,5}\.\d{1,5}$") || !Version.TryParse(tag[1..], out var version)) throw new InvalidDataException("A stable release version was not returned.");
        var name = $"ArcHive-{version}-win-x64.msi";
        var assets = root.GetProperty("assets").EnumerateArray().Where(a => a.GetProperty("name").GetString() == name).ToArray();
        if (assets.Length != 1) throw new InvalidDataException("This release has no unique Windows installer. View the releases page.");
        var asset = assets[0]; var size = asset.GetProperty("size").GetInt64();
        var digest = asset.TryGetProperty("digest", out var hash) ? hash.GetString() ?? "" : "";
        if (size <= 0 || size > MaximumPackage || !Regex.IsMatch(digest, @"^sha256:[0-9a-fA-F]{64}$")) throw new InvalidDataException("The release has no valid package size or SHA-256 digest.");
        var download = Repository + "/releases/download/" + tag + "/" + name;
        if (asset.GetProperty("browser_download_url").GetString() != download) throw new InvalidDataException("The installer is not from the ArcHive release.");
        return new(version, tag, root.TryGetProperty("body",out var body) ? (body.GetString() ?? "")[..Math.Min(body.GetString()?.Length ?? 0,20000)] : "", new(Repository + "/releases/tag/" + tag), new(download), name, size, digest[7..].ToLowerInvariant());
    }
    private async Task<HttpResponseMessage> Get(Uri uri, bool asset, CancellationToken token)
    {
        for (var hop = 0; hop < 5; hop++)
        {
            if (uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length != 0 || !(uri.Host == "api.github.com" || asset && uri.Host is "github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com")) throw new InvalidDataException("Update download redirected to an unexpected host.");
            using var request = new HttpRequestMessage(HttpMethod.Get, uri); request.Headers.UserAgent.ParseAdd(MetadataText.UserAgent);
            var response = await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);
            // The caller's HTTP handler must disable automatic redirects and cookies.
            if (response.RequestMessage?.RequestUri is { } final && final != uri) { response.Dispose(); throw new InvalidDataException("Unexpected automatic update redirect."); }
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                var location = response.Headers.Location; response.Dispose();
                if (!asset || location is null) throw new InvalidDataException("Unexpected update redirect.");
                uri = new(uri, location); continue;
            }
            if (!response.IsSuccessStatusCode) { var status = (int)response.StatusCode; response.Dispose(); throw new IOException($"GitHub update request failed (HTTP {status}). Try again later."); }
            return response;
        }
        throw new InvalidDataException("Too many update redirects.");
    }
    public async Task<string> DownloadAsync(AppRelease release, string folder, IProgress<double>? progress, CancellationToken token)
    {
        if (release.Version <= Current) throw new InvalidOperationException("Only newer releases can be installed.");
        var expected = Repository + "/releases/download/" + release.Tag + "/" + release.FileName;
        if (release.Download.AbsoluteUri != expected || release.FileName != $"ArcHive-{release.Version}-win-x64.msi" || release.Tag != "v" + release.Version || release.Size is <= 0 or > MaximumPackage || !Regex.IsMatch(release.Sha256,@"^[0-9a-fA-F]{64}$")) throw new InvalidDataException("Invalid update package.");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder,Guid.NewGuid().ToString("N") + "-" + release.FileName);
        var partial = path + ".partial";
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromMinutes(15));
            using var response = await Get(release.Download,true,timeout.Token);
            if (response.Content.Headers.ContentLength is { } length && length != release.Size) throw new InvalidDataException("Installer size does not match the release.");
            await using (var input = await response.Content.ReadAsStreamAsync(timeout.Token))
            await using (var output = new FileStream(partial,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true))
                await CopyLimited(input,output,release.Size,progress,timeout.Token);
            await VerifyAsync(partial,release,timeout.Token);
            File.Move(partial,path); return path;
        }
        finally { if (File.Exists(partial)) File.Delete(partial); }
    }
    public static async Task VerifyAsync(string path, AppRelease release, CancellationToken token)
    {
        await using var input = new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,81920,true);
        if (input.Length != release.Size || !string.Equals(Convert.ToHexString(await SHA256.HashDataAsync(input,token)),release.Sha256,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Installer verification failed. Download it again.");
    }
    private static async Task CopyLimited(Stream input, Stream output, long limit, IProgress<double>? progress, CancellationToken token)
    {
        var buffer = new byte[81920]; long total = 0; int count;
        while ((count = await input.ReadAsync(buffer,token)) > 0)
        { total += count; if (total > limit) throw new InvalidDataException("Update response exceeds its expected size."); await output.WriteAsync(buffer.AsMemory(0,count),token); progress?.Report((double)total / limit * 100); }
    }
}
