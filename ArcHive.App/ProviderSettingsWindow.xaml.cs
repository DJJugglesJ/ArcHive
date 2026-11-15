using System.Net.Http;
using System.Windows;
using ArcHive.Core;

namespace ArcHive.App;

internal static class ProviderSession
{
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
    private static readonly WindowsProviderCredentialStore Store = new();
    public static ProviderCredentials Credentials { get; private set; } = new();
    public static bool Remembered { get; private set; }
    private static bool loaded;
    private static readonly Dictionary<int, IMetadataProvider> Providers = [];
    public static void Load()
    {
        if (loaded) return;
        Credentials = Store.Load(); loaded = true;
        Remembered = new[] { Credentials.SteamKey, Credentials.IgdbClientId, Credentials.IgdbClientSecret, Credentials.ItchKey, Credentials.RawgKey }.Any(s => s.Length > 0);
    }
    public static void Save(ProviderCredentials credentials, bool remember)
    {
        if (remember) Store.Save(credentials); else Store.Delete();
        Credentials = credentials; Remembered = remember; loaded = true; Providers.Clear();
    }
    public static IMetadataProvider Get(int index)
    {
        if (Providers.TryGetValue(index, out var provider)) return provider;
        var c = Credentials;
        provider = index switch
        {
            0 => new VndbMetadataProvider(Client),
            1 => new SteamMetadataProvider(new(Client, ["api.steampowered.com"], TimeSpan.FromSeconds(1)), c.SteamKey),
            2 => new IgdbMetadataProvider(new(Client, ["api.igdb.com", "id.twitch.tv"]), c.IgdbClientId, c.IgdbClientSecret),
            3 => new ItchMetadataProvider(new(Client, ["api.itch.io"], TimeSpan.FromSeconds(1)), c.ItchKey),
            4 => new ItchMetadataProvider(new(Client, ["api.itch.io"], TimeSpan.FromSeconds(1)), c.ItchKey, projects: true),
            5 => new RawgMetadataProvider(new(Client, ["api.rawg.io"], TimeSpan.FromSeconds(1)), c.RawgKey),
            6 => new F95MetadataProvider(new F95ThreadSource(Client)),
            _ => throw new InvalidOperationException("Choose a provider.")
        };
        Providers[index] = provider; return provider;
    }
    public static IMetadataDetailsProvider? Details(IMetadataProvider provider) => provider.Id == "steam"
        ? new SteamStoreDetails(new(Client, ["store.steampowered.com"], TimeSpan.FromSeconds(1)))
        : provider as IMetadataDetailsProvider;

}

public partial class ProviderSettingsWindow : Window
{
    public ProviderSettingsWindow()
    {
        InitializeComponent(); Connections.Saved += (_, _) => DialogResult = true;
    }
}
