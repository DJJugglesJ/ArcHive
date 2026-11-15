using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ArcHive.Core;
namespace ArcHive.App;
public partial class SourceConnections : UserControl
{
    public event EventHandler? Saved;
    private static readonly string[] Sites = ["https://store.steampowered.com/login/", "https://dev.twitch.tv/console/apps", "https://itch.io/login", "https://rawg.io/login", "https://vndb.org/", "https://f95zone.to/", "https://www.gog.com/account"];
    private static readonly string[] KeySites = ["https://steamcommunity.com/dev/apikey", "https://dev.twitch.tv/console/apps", "https://itch.io/user/settings/api-keys", "https://rawg.io/apidocs"];
    public SourceConnections()
    {
        InitializeComponent();
        try { ProviderSession.Load(); } catch (ProviderException ex) { Message.Text = ex.Message; }
        Source.SelectedIndex = 0;
    }
    private ProviderCredentials Copy() => JsonSerializer.Deserialize<ProviderCredentials>(JsonSerializer.Serialize(ProviderSession.Credentials))!;
    private void SourceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (KeyEditor is null) return;
        var i = Source.SelectedIndex; var c = ProviderSession.Credentials;
        Key.Password = i switch { 0 => c.SteamKey, 1 => c.IgdbClientSecret, 2 => c.ItchKey, 3 => c.RawgKey, _ => "" };
        ClientId.Text = i == 1 ? c.IgdbClientId : "";
        KeyEditor.Visibility = KeyHelp.Visibility = i < 4 ? Visibility.Visible : Visibility.Collapsed;
        ClientEditor.Visibility = i == 1 ? Visibility.Visible : Visibility.Collapsed;
        KeyLabel.Text = i == 1 ? "Twitch client secret" : "API key";
        Remember.IsChecked = ProviderSession.Remembered || !new[] { c.SteamKey, c.IgdbClientSecret, c.ItchKey, c.RawgKey }.Any(v => v.Length > 0);
        Capability.Text = i switch {
            0 => "Steam catalog title search uses a Web API key. Selecting a match retrieves public store descriptions and genres. This is separate from your owned library.",
            1 => "IGDB title search, descriptions and artwork through your registered Twitch application.",
            2 => "Find game information in your owned itch.io games or projects using your own API key.",
            3 => "RAWG title search, full selected-game descriptions, tags and artwork using your API key. Attribution remains visible in ArcHive.",
            4 => "VNDB visual-novel title search, descriptions and non-spoiler tags. No account credentials required.",
            5 => "Select F95Zone in Find Game Information and paste a thread URL or ID. Review public descriptions, thread tags, engine and version/channel evidence. Enable it under Optional Sources first.",
            _ => "GOG account access stays in your browser. Add downloaded games and offline archives to ArcHive through Add games." };
        ConnectionState.Text = i < 4 ? (Key.Password.Length > 0 ? "Credentials configured · not verified" : "Not configured") : i == 4 ? "Available without a key" : "Browser access · no ArcHive account connection";
        Limitations.Text = i switch {
            0 => "A catalog lookup does not import owned games or sign you into Steam. Use the separate owned-library import option when wanted. Opening the Steam website does not mark ArcHive as connected.",
            5 => "No forum password or session cookie is collected. Private/login-required pages cannot be checked in ArcHive; use the browser. This is explicit thread lookup, not site-wide search or automatic downloads.",
            6 => "Owned-library synchronization requires a supported account integration. ArcHive does not embed another application's OAuth client or ask you to copy browser cookies.",
            _ => "Saving credentials makes no network request. A successful metadata lookup verifies access. Existing game details change only after you review and apply fields." };
    }
    private void Change(bool disconnect)
    {
        try
        {
            var c = Copy(); var key = disconnect ? "" : Key.Password.Trim();
            switch (Source.SelectedIndex) { case 0: c.SteamKey = key; break; case 1: c.IgdbClientSecret = key; c.IgdbClientId = disconnect ? "" : ClientId.Text.Trim(); break; case 2: c.ItchKey = key; break; case 3: c.RawgKey = key; break; default: return; }
            ProviderSession.Save(c, disconnect ? ProviderSession.Remembered : Remember.IsChecked == true);
            SourceChanged(this, null!); Message.Text = disconnect ? "Source disconnected. Other configured sources are preserved." : "Connection settings saved. Search a game to verify access.";
            Saved?.Invoke(this, EventArgs.Empty);
        }
        catch (ProviderException ex) { Message.Text = ex.Message; }
    }
    private void Save(object sender, RoutedEventArgs e) => Change(false);
    private void Disconnect(object sender, RoutedEventArgs e) => Change(true);
    private void OpenWebsite(object sender, RoutedEventArgs e) => Open(Sites[Source.SelectedIndex]);
    private void OpenKeyHelp(object sender, RoutedEventArgs e) { if (Source.SelectedIndex is >= 0 and < 4) Open(KeySites[Source.SelectedIndex]); }
    private void Open(string url) { try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); Message.Text = "Opened in your browser. Browser sign-in does not connect an account to ArcHive."; } catch { Message.Text = "The website could not be opened."; } }
}
