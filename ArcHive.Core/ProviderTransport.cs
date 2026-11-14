using System.Net;
using System.Text.Json;

namespace ArcHive.Core;

public sealed class ProviderException(string message, HttpStatusCode? status = null) : Exception(message)
{ public HttpStatusCode? Status { get; } = status; }

// One instance per provider/account. HTTP clients must disable redirects so credentials
// cannot follow a redirect. The desktop factory enforces this; tests inject a fake handler.
public sealed class ProviderTransport(HttpClient client, string[] hosts, TimeSpan? interval = null)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private DateTimeOffset nextRequest, backoff;
    public async Task<JsonDocument> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        using (request)
        {
            var uri = request.RequestUri;
            if (uri is null || uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length != 0 || !hosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
                throw new ProviderException("Provider endpoint is not allowed.");
            await gate.WaitAsync(token);
            try
            {
                if (DateTimeOffset.UtcNow < backoff) throw new ProviderException("Provider rate limit reached. Try again later.");
                var delay = nextRequest - DateTimeOffset.UtcNow;
                if (delay > TimeSpan.Zero) await Task.Delay(delay, token);
                nextRequest = DateTimeOffset.UtcNow + (interval ?? TimeSpan.FromMilliseconds(350));
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
                deadline.CancelAfter(TimeSpan.FromSeconds(20));
                request.Headers.UserAgent.ParseAdd("ArcHive/0.5.1");
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var retry = response.Headers.RetryAfter?.Delta ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow) ?? TimeSpan.FromMinutes(1);
                    backoff = DateTimeOffset.UtcNow.AddSeconds(Math.Clamp(retry.TotalSeconds, 2, 3600));
                }
                if (!response.IsSuccessStatusCode)
                    throw new ProviderException(response.StatusCode switch
                    {
                        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Provider rejected the credentials or their permissions. Check provider settings.",
                        HttpStatusCode.TooManyRequests => "Provider rate limit reached. Try again later.",
                        _ => $"Provider request failed (HTTP {(int)response.StatusCode})."
                    }, response.StatusCode);
                using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
                using var buffer = new MemoryStream(); var block = new byte[8192]; int count;
                while ((count = await stream.ReadAsync(block, deadline.Token)) > 0)
                {
                    if (buffer.Length + count > 2_000_000) throw new ProviderException("Provider response exceeds 2 MB.");
                    buffer.Write(block, 0, count);
                }
                return JsonDocument.Parse(buffer.ToArray());
            }
            catch (OperationCanceledException) { throw; }
            catch (ProviderException) { throw; }
            // Never expose request URLs, response bodies or exception details containing keys.
            catch (Exception) { throw new ProviderException("Provider connection or response could not be read."); }
            finally { gate.Release(); }
        }
    }
}

public abstract class CachedMetadataProvider : IMetadataProvider
{
    public abstract string Id { get; }
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, (DateTimeOffset At, MetadataCandidate[] Items)> cache = [];
    public async Task<IReadOnlyList<MetadataCandidate>> SearchAsync(MetadataQuery query, CancellationToken token)
    {
        var title = query.Title.Trim();
        if (title.Length is < 2 or > 200 || title.Any(char.IsControl)) throw new ProviderException("Enter a search title of 2–200 characters, without control characters.");
        await gate.WaitAsync(token);
        try
        {
            if (cache.TryGetValue(title, out var cached) && DateTimeOffset.UtcNow - cached.At < TimeSpan.FromMinutes(10)) return Clone(cached.Items);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromMinutes(3));
            var items = await Task.Run(() => FetchAsync(title, deadline.Token), deadline.Token);
            deadline.Token.ThrowIfCancellationRequested();
            foreach (var item in items) MetadataValidation.Validate(item);
            if (cache.Count >= 100) cache.Clear();
            cache[title] = (DateTimeOffset.UtcNow, Clone(items)); return items;
        }
        catch (OperationCanceledException) { throw; }
        catch (ProviderException) { throw; }
        catch (Exception) { throw new ProviderException("Provider returned invalid metadata. No changes were applied."); }
        finally { gate.Release(); }
    }
    protected abstract Task<MetadataCandidate[]> FetchAsync(string title, CancellationToken token);
    protected static MetadataCandidate[] Clone(MetadataCandidate[] items) => items.Select(c => c with { Aliases = c.Aliases.ToArray(), Fields = new(c.Fields), Artwork = c.Artwork.ToArray() }).ToArray();
    protected static string Required(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 1024 || value.Any(char.IsControl)) throw new ProviderException("Configure valid credentials for this provider first.");
        return value.Trim();
    }
    protected static string Text(JsonElement row, string name) => row.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString()! : "";
    protected static string NumericId(JsonElement row, string name = "id")
    {
        if (!row.GetProperty(name).TryGetInt64(out var id) || id <= 0) throw new ProviderException("Provider returned an invalid identifier.");
        return id.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
    protected static string[] Names(JsonElement row, string field) => row.TryGetProperty(field, out var p) && p.ValueKind == JsonValueKind.Array
        ? p.EnumerateArray().Select(x => Text(x, "name")).Where(x => x.Length > 0).ToArray() : [];
    protected static void Year(Dictionary<MetadataField, string> fields, string date)
    { if (date.Length >= 4 && int.TryParse(date[..4], out var year) && year is >= 1000 and <= 9999) fields[MetadataField.ReleaseYear] = date[..4]; }
    protected static JsonElement Rows(JsonElement root, string name, int max)
    {
        var rows = root.GetProperty(name);
        if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() > max) throw new ProviderException("Provider returned an invalid result count.");
        return rows;
    }
    protected static MetadataCandidate[] Best(string title, IEnumerable<MetadataCandidate> items) => MetadataMatching.Rank(new(title), items)
        .Where(m => m.Score > 0).Take(20).Select(m => m.Candidate).ToArray();
}
