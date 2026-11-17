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
                using var request = new HttpRequestMessage(HttpMethod.Get, destination); request.Headers.UserAgent.ParseAdd(MetadataText.UserAgent);
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
    public static void ApplyEvidence(Game game, MetadataCandidate candidate, bool useRelease)
    {
        MetadataValidation.Validate(candidate);
        if (candidate.Provider != "f95zone") throw new ProviderException("Select an F95Zone match.");
        var url = F95MetadataProvider.Locator(candidate.Id);
        game.F95Thread = new() { Url = url, CheckedAt = candidate.RetrievedAt, ObservedTitle = candidate.Title,
            ObservedVersion = candidate.ObservedRelease is { } observed ? (observed.Version + " " + observed.Channel).Trim() : game.F95Thread?.Url == url ? game.F95Thread.ObservedVersion : "" };
        if (useRelease && candidate.ObservedRelease is { } release)
        {
            game.LatestRelease = release with { }; game.LatestReleaseSource = url; game.LatestReleaseCheckedAt = candidate.RetrievedAt;
            VersionTracking.Record(game, game.LatestRelease, url);
        }
    }
    public static F95ThreadResult Parse(string url, string html)
    {
        url = CanonicalUrl(url);
        if (html.Length > 2_000_000) throw new ProviderException("Thread page exceeds the lookup limit.");
        string Clean(string value) => MetadataText.Plain(value);
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
        var versionMatch = Regex.Match(raw, @"\[(?<version>\d{4}-\d{2}-\d{2}|v?\d+(?:\.\d+){0,7}(?:[a-zA-Z][a-zA-Z0-9.-]*)?)(?:[ \t]+(?<channel>[^\]\r\n]{1,80}))?\]", RegexOptions.IgnoreCase, RegexTimeout);
        var version = versionMatch.Success ? versionMatch.Value.Trim('[', ']') : "";
        var title = Regex.Replace(raw, @"\s*\|\s*F95zone.*$", "", RegexOptions.IgnoreCase, RegexTimeout);
        title = Regex.Replace(title, @"\[[^\]]*\]", "", RegexOptions.None, RegexTimeout).Trim();
        title = Regex.Replace(title, @"^(?:(?:Completed|Abandoned|On Hold)\s*[-\u2013]\s*)+", "", RegexOptions.IgnoreCase, RegexTimeout);
        var enginePrefix = Regex.Match(title, @"^(Ren'Py|Unity|RPGM|Godot|Unreal Engine)\s*[-–]\s*", RegexOptions.IgnoreCase, RegexTimeout);
        if (enginePrefix.Success) title = title[enginePrefix.Length..].Trim();
        title = Regex.Replace(title, @"^(?:(?:Completed|Abandoned|On Hold)\s*[-\u2013]\s*)+", "", RegexOptions.IgnoreCase, RegexTimeout);
        var fields = new Dictionary<MetadataField, string>(); var description = Clean(Meta("og:description")); if (description.Length > 0) fields[MetadataField.Description] = description[..Math.Min(description.Length, 10000)];
        var engine = Regex.Match(raw, @"\[(Ren'Py|Unity|RPGM|Godot|Unreal Engine)\]", RegexOptions.IgnoreCase, RegexTimeout); if (engine.Success) fields[MetadataField.Engine] = engine.Groups[1].Value; else if (enginePrefix.Success) fields[MetadataField.Engine] = enginePrefix.Groups[1].Value;
        // XenForo thread labels/tags live before the first post. Never collect reply tags.
        var firstArticle = Regex.Match(html, @"<article\b", RegexOptions.IgnoreCase, RegexTimeout);
        var header = firstArticle.Success ? html[..firstArticle.Index] : html;
        var tagNames = new List<string>();
        foreach (Match anchor in Regex.Matches(header, @"<a\b(?<attrs>[^>]{0,8192})>(?<text>.*?)</a\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline, RegexTimeout))
        {
            var attributes = anchor.Groups["attrs"].Value;
            if (Regex.IsMatch(attributes, "class\\s*=\\s*([\"'])[^\"']*\\btagItem\\b[^\"']*\\1|rel\\s*=\\s*([\"'])tag\\2", RegexOptions.IgnoreCase, RegexTimeout))
                tagNames.Add(Clean(anchor.Groups["text"].Value));
            else if (Regex.IsMatch(attributes, @"href\s*=\s*[""''](?:https://f95zone\.to)?/tags/[^""'']+[""'']", RegexOptions.IgnoreCase, RegexTimeout))
                tagNames.Add(Clean(anchor.Groups["text"].Value));
        }
        var tagValue = MetadataText.Tags(tagNames); if (tagValue.Length > 0) fields[MetadataField.Tags] = tagValue;
        // Prefer the first post's explicit Overview section to the truncated social preview.
        var article = Regex.Match(html, @"<article\b[^>]*>(?<body>.*?)</article\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline, RegexTimeout);
        if (article.Success)
        {
            var body = Clean(article.Groups["body"].Value);
            var overview = Regex.Match(body, @"(?:^|\n)Overview\s*:[ \t]*(?<text>.*?)(?=\n(?:Thread Updated|Release Date|Developer|Censored|Version|OS|Language|Genre|Tags|Installation|Changelog|Downloads?|Screenshots?)[ \t]*:|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline, RegexTimeout);
            var full = overview.Groups["text"].Value.Trim(); if (full.Length > 0) fields[MetadataField.Description] = full[..Math.Min(full.Length, 20000)];
        }
        var candidate = new MetadataCandidate("f95zone", new Uri(url).Segments[^1].Trim('/'), title, [], fields, DateTimeOffset.UtcNow)
        { ObservedRelease = versionMatch.Success ? new ReleaseInfo { Version = versionMatch.Groups["version"].Value, Channel = versionMatch.Groups["channel"].Value.Trim(), OriginalName = raw } : null };
        MetadataValidation.Validate(candidate);
        return new(url, candidate, version);
    }
}


public sealed class F95MetadataProvider(F95ThreadSource source) : IMetadataProvider
{
    public string Id => "f95zone";
    public static string Locator(string input)
    {
        var value = input.Trim();
        if (Regex.IsMatch(value, @"^[1-9][0-9]{0,11}$")) value = "https://f95zone.to/threads/" + value + "/";
        return F95ThreadSource.CanonicalUrl(value);
    }
    public async Task<IReadOnlyList<MetadataCandidate>> SearchAsync(MetadataQuery query, CancellationToken cancellationToken)
    {
        var result = await source.CheckAsync(Locator(query.Title), cancellationToken); return [result.Candidate];
    }
}
