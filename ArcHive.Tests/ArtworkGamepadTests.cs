using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ArcHive.App;
using ArcHive.Core;

internal static class ArtworkGamepadTests
{
    public static async Task Run(string root, Action<bool, string> check)
    {
        var at = DateTimeOffset.UtcNow;
        ArtworkReference Art(string name = "cover") => new(ArtworkKind.Cover, "https://images.igdb.com/igdb/image/upload/t_cover_big/" + name + ".jpg", "igdb", "42", at);
        using var igdbJson = JsonDocument.Parse("""{"cover":{"image_id":"abc"},"artworks":[{"image_id":"back"}],"screenshots":[{"image_id":"screen"}]}""");
        var images = ProviderArtwork.Parse("igdb", "42", igdbJson.RootElement, at);
        check(images.Length == 3 && images.Select(a => a.Kind).Distinct().Count() == 3 && images[0].Url.EndsWith("t_cover_big/abc.jpg"), "IGDB artwork maps three independent roles");
        using var rawgJson = JsonDocument.Parse("""{"background_image":"https://media.rawg.io/media/back.jpg","short_screenshots":[{"image":"https://media.rawg.io/media/shot.png"}]}""");
        check(ProviderArtwork.Parse("rawg", "1", rawgJson.RootElement, at).Select(a => a.Kind).SequenceEqual(new[] { ArtworkKind.Backdrop, ArtworkKind.Screenshot }), "RAWG maps backdrop and screenshot URLs");
        using var itchJson = JsonDocument.Parse("""{"cover_url":"http://img.itch.zone/cover.png"}""");
        check(ProviderArtwork.Parse("itch", "1", itchJson.RootElement, at).Single().Url == "https://img.itch.zone/cover.png", "itch artwork upgrades documented CDN HTTP URL to HTTPS");
        using var vndbJson = JsonDocument.Parse("""{"image":{"url":"https://t.vndb.org/cv/1.jpg"},"screenshots":[{"url":"https://s.vndb.org/sf/2.jpg"}]}""");
        check(ProviderArtwork.Parse("vndb", "v1", vndbJson.RootElement, at).Length == 2, "VNDB maps cover and screenshots");
        check(ProviderArtwork.Parse("steam", "42", igdbJson.RootElement, at).Length == 0, "Steam catalog does not invent artwork URLs");
        using var badJson = JsonDocument.Parse("""{"cover":{"image_id":"../private"},"screenshots":[{"image_id":"bad?query"}]}""");
        check(ProviderArtwork.Parse("igdb", "42", badJson.RootElement, at).Length == 0, "Invalid provider image IDs are ignored");
        foreach (var url in new[] { "http://images.igdb.com/x.jpg", "https://127.0.0.1/x.jpg", "https://images.igdb.com.evil.invalid/x.jpg", "https://user@images.igdb.com/x.jpg", "https://images.igdb.com:8443/x.jpg", "https://images.igdb.com/x.jpg?key=secret", "file:///C:/a.jpg" })
            await Reject(() => { ProviderArtwork.Validate(Art() with { Url = url }); return Task.CompletedTask; }, "Untrusted artwork URL rejected", check);
        var candidate = new MetadataCandidate("igdb", "42", "Moon Garden", [], [], at) { Artwork = images };
        await Reject(() => { MetadataValidation.Validate(candidate with { Artwork = [Art() with { ProviderId = "other" }] }); return Task.CompletedTask; }, "Artwork candidate ownership validated", check);
        var game = new Game { Title = "Moon Garden", ScreenshotPath = "legacy-image.png" };
        ProviderArtwork.Select(game, images[0], ArtworkKind.Cover); ProviderArtwork.Select(game, images[1], ArtworkKind.Backdrop);
        ProviderArtwork.Select(game, images[2], ArtworkKind.Screenshot); ProviderArtwork.Select(game, images[2], ArtworkKind.Screenshot);
        check(game.Artwork.Cover == images[0] && game.Artwork.Backdrop == images[1] && game.Artwork.Screenshots.Count == 1 && game.ScreenshotPath == "legacy-image.png", "Artwork role selection preserves independent images and local fallback");
        check(!game.HasControllerSupport, "Unknown controller support does not display badge");
        MetadataReconciliation.SetManual(game, MetadataField.ControllerSupport, "Partial");
        check(game.HasControllerSupport && game.Metadata.Fields[MetadataField.ControllerSupport].Manual, "Manual partial controller support displays badge and provenance");
        var supportCandidate = candidate with { Fields = new() { [MetadataField.ControllerSupport] = "Full" } };
        MetadataReconciliation.Apply(game, supportCandidate, MetadataReconciliation.Preview(game, supportCandidate).Where(p => p.Field == MetadataField.ControllerSupport).ToArray());
        check(game.ControllerSupport == ControllerSupport.Full, "Provider controller support requires explicit field selection");
        MetadataReconciliation.Undo(game); check(game.ControllerSupport == ControllerSupport.Partial, "Controller support participates in protected undo");
        var store = new SqliteLibraryStore(Path.Combine(root, "artwork.db")); store.Load(); store.Save(new Library { Games = [game] });
        var restored = store.Load().Games.Single();
        check(restored.Artwork.Cover == images[0] && restored.Artwork.Backdrop == images[1] && restored.Artwork.Screenshots.Single() == images[2] && restored.ControllerSupport == ControllerSupport.Partial, "SQLite retains artwork roles source identity and controller support");
        var legacy = JsonSerializer.Deserialize<Game>("{\"Title\":\"Legacy\",\"ScreenshotPath\":\"old.png\"}")!;
        check(legacy.Artwork.Cover is null && legacy.ScreenshotPath == "old.png" && legacy.ControllerSupport == ControllerSupport.Unknown, "Legacy image and unknown controller defaults preserved");
        var notifications = 0; game.PropertyChanged += (_, e) => { if (e.PropertyName == "IsSelected") notifications++; }; game.IsSelected = true; game.IsSelected = true;
        check(notifications == 1 && !JsonSerializer.Serialize(game).Contains("IsSelected"), "Selection highlight notifies once and is not persisted");

        var png = Encode(new PngBitmapEncoder()); var jpeg = Encode(new JpegBitmapEncoder()); var bmp = Encode(new BmpBitmapEncoder());
        check(ArtworkImage.Dimensions(png) == (4, 3) && ArtworkImage.Dimensions(jpeg) == (4, 3), "PNG and JPEG dimensions parsed before decode");
        check(ArtworkImages.Decode(png).IsFrozen && ArtworkImages.Decode(jpeg).PixelHeight == 3, "Real WPF PNG and JPEG decoding succeeds without UI");
        check(ArtworkImages.Decode(bmp, true).PixelWidth == 4, "Existing local BMP artwork remains supported");
        var bomb = png.ToArray(); BinaryPrimitives.WriteInt32BigEndian(bomb.AsSpan(16, 4), 9000);
        await Reject(() => ArtworkImages.Validate(bomb, default), "Oversized image dimensions rejected before decoder", check);
        await Reject(() => ArtworkImages.Validate(png[..33], default), "Truncated image rejected by actual decoder", check);
        await Reject(() => ArtworkImages.Validate("<html>not an image</html>"u8.ToArray(), default), "Non-image bytes rejected", check);
        var badBmp = bmp.ToArray(); BitConverter.GetBytes(int.MinValue).CopyTo(badBmp, 22);
        await Reject(() => { ArtworkImages.Decode(badBmp, true); return Task.CompletedTask; }, "Invalid local bitmap height cannot overflow", check);

        var handler = new Images(png); using var client = new HttpClient(handler);
        var directory = Path.Combine(root, "art-cache"); var cache = new ArtworkCache(directory, client, ArtworkImages.Validate, png.Length * 2, png.Length);
        var simultaneous = await Task.WhenAll(cache.GetAsync(Art(), default), cache.GetAsync(Art(), default));
        check(handler.Calls == 1 && simultaneous.All(b => b.SequenceEqual(png)), "Concurrent artwork requests reuse one download");
        check(handler.Authorization is null && handler.RequestUri == new Uri(Art().Url), "Artwork downloader sends no provider credentials");
        check(Directory.GetFiles(directory, "*.image").Length == 1 && Directory.GetFiles(directory, "*.tmp").Length == 0, "Artwork cache publishes atomically and removes staging");
        using var offlineClient = new HttpClient(new Images(png) { Offline = true });
        check((await new ArtworkCache(directory, offlineClient, ArtworkImages.Validate, png.Length * 2, png.Length).GetAsync(Art(), default)).SequenceEqual(png), "Cached artwork remains usable offline and across cache instances");
        await cache.GetAsync(Art("two"), default);
        var firstPath = Path.Combine(directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Art().Url))).ToLowerInvariant() + ".image");
        File.SetLastWriteTimeUtc(firstPath, DateTime.UtcNow.AddHours(-1));
        await cache.GetAsync(Art("three"), default);
        check(!File.Exists(firstPath) && Directory.GetFiles(directory, "*.image").Sum(f => new FileInfo(f).Length) <= png.Length * 2, "Artwork LRU eviction respects disk budget");
        await cache.GetAsync(Art(), default); check(handler.Calls == 4, "Evicted artwork is downloaded again on demand");
        await File.WriteAllTextAsync(firstPath, "corrupt cached image");
        await cache.GetAsync(Art(), default); check(handler.Calls == 5, "Corrupt cache entries are validated and repaired");
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        try { await cache.GetAsync(Art(), canceled.Token); throw new Exception("Cancellation ignored"); }
        catch (OperationCanceledException) { check(true, "Artwork cancellation before cache access"); }
        var slow = new Images(png) { Slow = true }; using var slowClient = new HttpClient(slow); using var during = new CancellationTokenSource(40);
        var slowDir = Path.Combine(root, "art-slow");
        try { await new ArtworkCache(slowDir, slowClient, ArtworkImages.Validate).GetAsync(Art(), during.Token); throw new Exception("Cancellation ignored"); }
        catch (OperationCanceledException) { check(!Directory.GetFiles(slowDir).Any(), "Canceled download leaves no cache or staging file"); }
        foreach (var fault in new[] { "type", "redirect", "size", "decode", "offline" })
        {
            var bad = new Images(fault == "size" ? new byte[png.Length + 1] : fault == "decode" ? "not an image"u8.ToArray() : png)
            { Mime = fault == "type" ? "text/html" : "image/png", Status = fault == "redirect" ? HttpStatusCode.Redirect : HttpStatusCode.OK, Offline = fault == "offline" };
            using var badClient = new HttpClient(bad); var badDir = Path.Combine(root, "art-" + fault);
            var badCache = new ArtworkCache(badDir, badClient, ArtworkImages.Validate, png.Length * 2, png.Length);
            await Reject(() => badCache.GetAsync(Art(), default), "Artwork " + fault + " failure retains no cache entry", check);
            check(!Directory.GetFiles(badDir).Any(), "Failed artwork leaves no disk residue");
            await Reject(() => badCache.GetAsync(Art(), default), "Artwork failure backoff is surfaced", check);
            check(bad.Calls == 1, "Artwork failure backoff prevents repeated requests");
        }
        var rawgHandler = new JsonHandler("""{"results":[{"id":1,"name":"Pad Game","tags":[{"slug":"full-controller-support","name":"Full controller support"}],"background_image":"https://media.rawg.io/media/art.jpg"}]}""");
        using var rawgClient = new HttpClient(rawgHandler);
        var rawg = new RawgMetadataProvider(new(rawgClient, ["api.rawg.io"], TimeSpan.Zero), "fixture");
        var result = await rawg.SearchAsync(new("Pad Game"), default);
        check(result[0].Fields[MetadataField.ControllerSupport] == "Full" && result[0].Artwork.Single().Kind == ArtworkKind.Backdrop, "Provider integration returns explicit controller hint and artwork together");
        result[0].Artwork[0] = Art();
        check((await rawg.SearchAsync(new("Pad Game"), default))[0].Artwork[0].Provider == "rawg", "Provider cache isolates artwork array mutation");

        var pad = new GamepadNavigation(); var time = TimeSpan.Zero;
        PadAction Step(PadButtons b = 0, bool enabled = true, int milliseconds = 0, short x = 0, short y = 0)
        { time += TimeSpan.FromMilliseconds(milliseconds); return pad.Update(b, x, y, enabled, time); }
        check(Step(PadButtons.A) == PadAction.None && Step() == PadAction.None && Step(PadButtons.A) == PadAction.Accept, "Gamepad requires neutral state before accepting initial input");
        check(Step(PadButtons.A, milliseconds: 1000) == PadAction.None, "Holding A never repeats launch");
        Step(); check(Step(PadButtons.B) == PadAction.Cancel, "Gamepad B cancels");
        Step(); check(Step(PadButtons.Next) == PadAction.Next, "Gamepad shoulder switches view");
        Step(); check(Step(PadButtons.Right) == PadAction.Right && Step(PadButtons.Right, milliseconds: 449) == PadAction.None && Step(PadButtons.Right, milliseconds: 1) == PadAction.Right, "Directional repeat waits 450 milliseconds");
        check(Step(PadButtons.Right, milliseconds: 119) == PadAction.None && Step(PadButtons.Right, milliseconds: 1) == PadAction.Right, "Directional repeat interval is bounded");
        Step(); check(Step(x: 8000) == PadAction.None && Step(x: short.MinValue) == PadAction.Left, "Analog dead zone and signed extremes handled");
        check(Step(PadButtons.A, enabled: false) == PadAction.None && Step(PadButtons.A) == PadAction.None, "Focus loss editing disconnect or busy gate requires neutral rearming");
        Step(); check(Step(PadButtons.A) == PadAction.Accept, "Gamepad accepts fresh press after rearming");
        var confirmation = new GamepadNavigation();
        check(confirmation.Update(PadButtons.A, 0, 0, true, time) == PadAction.None && confirmation.Update(0, 0, 0, true, time) == PadAction.None && confirmation.Update(PadButtons.A, 0, 0, true, time) == PadAction.Accept, "Launch confirmation cannot consume the held opener press");
        check(GamepadNavigation.Move(-1, 0, 4, PadAction.Down) == -1 && GamepadNavigation.Move(-1, 10, 4, PadAction.Right) == 0, "Gamepad handles empty and unselected libraries");
        check(GamepadNavigation.Move(3, 10, 4, PadAction.Right) == 3 && GamepadNavigation.Move(4, 10, 4, PadAction.Left) == 4 && GamepadNavigation.Move(8, 10, 4, PadAction.Down) == 9, "Gamepad grid navigation clamps row and final-page edges");
        check(GamepadNavigation.Move(2, 10, 4, PadAction.Down) == 6 && GamepadNavigation.Move(6, 10, 4, PadAction.Up) == 2, "Gamepad follows responsive column count");
        XInputPad.Read(); check(true, "XInput polling tolerates disconnected controllers");
    }
    private static byte[] Encode(BitmapEncoder encoder)
    {
        var pixels = Enumerable.Repeat((byte)128, 4 * 3 * 3).ToArray();
        encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(4, 3, 96, 96, PixelFormats.Rgb24, null, pixels, 12)));
        using var memory = new MemoryStream(); encoder.Save(memory); return memory.ToArray();
    }
    private static async Task Reject(Func<Task> action, string name, Action<bool, string> check)
    {
        try { await action(); throw new Exception("Expected rejection: " + name); }
        catch (Exception ex) when (ex is InvalidDataException or IOException or NotSupportedException) { check(true, name); }
    }
    private sealed class Images(byte[] bytes) : HttpMessageHandler
    {
        public int Calls; public string? Authorization; public Uri? RequestUri;
        public bool Slow { get; init; } public bool Offline { get; init; }
        public string Mime { get; init; } = "image/png"; public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++; Authorization = request.Headers.Authorization?.ToString(); RequestUri = request.RequestUri;
            if (Offline) throw new HttpRequestException("Simulated offline");
            if (Slow) await Task.Delay(Timeout.Infinite, token);
            var content = new ByteArrayContent(bytes); content.Headers.ContentType = new MediaTypeHeaderValue(Mime);
            return new(Status) { Content = content };
        }
    }
    private sealed class JsonHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
    }
}
