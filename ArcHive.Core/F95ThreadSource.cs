using System.Net;
using System.Text;
using System.Text.RegularExpressions;
namespace ArcHive.Core;
public sealed class F95ThreadLink
{
    public string Url { get; set; } = "";
    public DateTimeOffset? CheckedAt { get; set; }
    public string ObservedTitle { get; set; } = "";
    public string ObservedVersion { get; set; } = "";
}
public sealed record F95ThreadResult(string Url, MetadataCandidate Candidate, string Version);
public sealed class F95ThreadSource(HttpClient client)
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(200);
    private readonly SemaphoreSlim gate = new(1, 1);
    private DateTimeOffset next;
    public static string CanonicalUrl(string input)
    {
        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "f95zone.to" || !uri.IsDefaultPort || uri.UserInfo.Length > 0 || uri.OriginalString.Split('?')[0].Contains('%')) throw new ProviderException("Use an HTTPS thread link on f95zone.to.");
        var match = Regex.Match(uri.AbsolutePath, @"^/threads/(?:[a-zA-Z0-9_-]+\.)?([1-9][0-9]{0,11})(?:/(?:page-[0-9]+)?)?$", RegexOptions.None, RegexTimeout);
        if (!match.Success) throw new ProviderException("Paste a thread URL, not a search, download or account link.");
        return "https://f95zone.to/threads/" + match.Groups[1].Value + "/";
    }
    public async Task<F95ThreadResult> CheckAsync(string url, CancellationToken token)
    {
        url = CanonicalUrl(url); await gate.WaitAsync(token);
        try
        {
            var delay = next - DateTimeOffset.UtcNow; if (delay > TimeSpan.Zero) await Task.Delay(delay, token);
            next = DateTimeOffset.UtcNow.AddSeconds(3);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(25));
            var destination = new Uri(url);
            for (var redirects = 0; redirects <= 2; redirects++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, destination); request.Headers.UserAgent.ParseAdd("ArcHive/0.11.0");
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                if ((int)response.StatusCode is >= 300 and < 400)
                {
                    if (redirects == 2 || response.Headers.Location is null) throw new ProviderException("Thread redirect limit reached.");
                    var redirected = new Uri(destination, response.Headers.Location);
                    if (redirected.Query.Length > 0 || CanonicalUrl(redirected.AbsoluteUri) != url) throw new ProviderException("Thread redirected away from the requested public page. Open it in your browser.");
                    destination = redirected; continue;
                }
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == HttpStatusCode.TooManyRequests) next = DateTimeOffset.UtcNow.AddMinutes(1);
                    throw new ProviderException(response.StatusCode == HttpStatusCode.TooManyRequests ? "F95Zone rate limit reached. Try again later." : "This thread is unavailable to public lookup. Open it in your browser; ArcHive does not use browser login cookies.");
                }
                if (response.RequestMessage?.RequestUri is { } final && CanonicalUrl(final.AbsoluteUri) != url) throw new ProviderException("Unexpected source destination.");
                if (response.Content.Headers.ContentType?.MediaType != "text/html") throw new ProviderException("F95Zone did not return a thread page.");
                using var input = await response.Content.ReadAsStreamAsync(timeout.Token); using var output = new MemoryStream(); var buffer = new byte[8192]; int count;
                while ((count = await input.ReadAsync(buffer, timeout.Token)) > 0) { if (output.Length + count > 2_000_000) throw new ProviderException("Thread page exceeds the lookup limit."); output.Write(buffer, 0, count); }
                return Parse(url, Encoding.UTF8.GetString(output.ToArray()));
            }
            throw new ProviderException("Thread redirect limit reached.");
        }
        catch (OperationCanceledException) { throw; }
        catch (ProviderException) { throw; }
        catch { throw new ProviderException("Thread lookup failed. No library fields were changed."); }
        finally { gate.Release(); }
    }
    public static F95ThreadResult Parse(string url, string html)
    {
        url = CanonicalUrl(url);
        if (html.Length > 2_000_000) throw new ProviderException("Thread page exceeds the lookup limit.");
        string Clean(string value) => Regex.Replace(Regex.Replace(WebUtility.HtmlDecode(value), "<[^>]*>", "", RegexOptions.Singleline, RegexTimeout), @"\s+", " ", RegexOptions.None, RegexTimeout).Trim();
        string Meta(string key)
        {
            foreach (Match tag in Regex.Matches(html, @"<meta\b[^>]{0,10000}>", RegexOptions.IgnoreCase, RegexTimeout))
            {
                var attributes = Regex.Matches(tag.Value, "([a-zA-Z-]+)\\s*=\\s*([\"'])(.*?)\\2", RegexOptions.Singleline, RegexTimeout).Cast<Match>().GroupBy(m => m.Groups[1].Value.ToLowerInvariant()).ToDictionary(g => g.Key, g => g.Last().Groups[3].Value);
                if ((attributes.GetValueOrDefault("property") ?? attributes.GetValueOrDefault("name")) == key) return attributes.GetValueOrDefault("content") ?? "";
            }
            return "";
        }
        var raw = Meta("og:title");
        if (raw.Length == 0) raw = Regex.Match(html, @"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline, RegexTimeout).Groups[1].Value;
        raw = Clean(raw);
        if (raw.Length == 0 || raw.Length > 700 || Regex.IsMatch(raw, "just a moment|log in|sign in|access denied|attention required|security check", RegexOptions.IgnoreCase, RegexTimeout)) throw new ProviderException("A readable public thread title was not returned. Open the thread in your browser.");
        var canonical = Meta("og:url");
        if (canonical.Length == 0 || CanonicalUrl(WebUtility.HtmlDecode(canonical)) != url) throw new ProviderException("The page does not identify the requested thread.");
        var version = Regex.Match(raw, @"\[(v?\d+(?:\.\d+){0,7}(?:[a-zA-Z][a-zA-Z0-9.-]*)?)\]", RegexOptions.IgnoreCase, RegexTimeout).Groups[1].Value;
        var title = Regex.Replace(raw, @"\s*\|\s*F95zone.*$", "", RegexOptions.IgnoreCase, RegexTimeout);
        title = Regex.Replace(title, @"\[[^\]]*\]", "", RegexOptions.None, RegexTimeout).Trim();
        var fields = new Dictionary<MetadataField, string>(); var description = Clean(Meta("og:description")); if (description.Length > 0) fields[MetadataField.Description] = description[..Math.Min(description.Length, 10000)];
        var engine = Regex.Match(raw, @"\[(Ren'Py|Unity|RPGM|Godot|Unreal Engine)\]", RegexOptions.IgnoreCase, RegexTimeout); if (engine.Success) fields[MetadataField.Engine] = engine.Groups[1].Value;
        var candidate = new MetadataCandidate("f95zone", new Uri(url).Segments[^1].Trim('/'), title, [], fields, DateTimeOffset.UtcNow); MetadataValidation.Validate(candidate);
        return new(url, candidate, version);
    }
}
