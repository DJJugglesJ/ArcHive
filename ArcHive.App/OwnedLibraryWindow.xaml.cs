using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ArcHive.Core;
namespace ArcHive.App;
public partial class OwnedLibraryWindow : Window
{
    private readonly Library library;
    private readonly Action<Library> save;
    private CancellationTokenSource? pending;
    public OwnedLibraryWindow(Library library, Action<Library> save)
    {
        InitializeComponent(); this.library = library; this.save = save; ProviderChanged(this, null!);
        Closing += (_, e) => { if (pending is not null) { pending.Cancel(); e.Cancel = true; Message.Text = "Canceling lookup. Close again when it finishes."; } };
    }
    private void ProviderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Fetch is null) return; Fetch.IsEnabled = SteamId.IsEnabled = Provider.SelectedIndex == 0; Games.ItemsSource = null; ApplyButton.IsEnabled = false;
        Message.Text = Provider.SelectedIndex == 0 ? "Fetch with your configured Steam key and SteamID64, or import Steam response JSON. Your profile's game details must be visible to the API." : "Import GOG library JSON containing products with id and title. Browser sign-in does not provide ArcHive account access; authenticated GOG synchronization is not available.";
    }
    private void Show(IReadOnlyList<OwnedGame> games)
    {
        var fresh = games.Where(item => !library.Games.Any(g => g.OwnedLibraryIds.GetValueOrDefault(item.Provider) == item.Id)).ToArray();
        Games.ItemsSource = fresh; ApplyButton.IsEnabled = fresh.Length > 0; Message.Text = $"{fresh.Length} new titles; {games.Count - fresh.Length} already linked. Select the titles to add. Title matches alone are not merged.";
    }
    private async void FetchSteam(object sender, RoutedEventArgs e)
    {
        if (pending is not null) return; pending = new(); Actions.IsEnabled = ApplyButton.IsEnabled = false; Games.ItemsSource = null;
        try { ProviderSession.Load(); using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }); Show(await OwnedLibraryImport.FetchSteamAsync(new(client, ["api.steampowered.com"]), ProviderSession.Credentials.SteamKey, SteamId.Text.Trim(), pending.Token)); }
        catch (Exception ex) { Message.Text = ex is OperationCanceledException ? "Lookup canceled or timed out." : ex is ProviderException ? ex.Message : "Steam library response could not be read."; }
        finally { pending.Dispose(); pending = null; Actions.IsEnabled = true; }
    }
    private void ImportFile(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Filter = "Library JSON|*.json" };
        if (picker.ShowDialog(this) != true) return;
        try { if (new FileInfo(picker.FileName).Length > 2_000_000) throw new InvalidDataException(); Show(OwnedLibraryImport.Parse(File.ReadAllText(picker.FileName), Provider.SelectedIndex == 0 ? "steam" : "gog")); }
        catch { Message.Text = "This file is not a supported library export (maximum 2 MB). No games were added."; }
    }
    private void SelectAll(object sender, RoutedEventArgs e) => Games.SelectAll();
    private void Apply(object sender, RoutedEventArgs e)
    {
        if (pending is not null || Games.SelectedItems.Count == 0) { Message.Text = "Select titles first."; return; }
        try { save(OwnedLibraryImport.Apply(library, Games.SelectedItems.Cast<OwnedGame>())); DialogResult = true; } catch { Message.Text = "Library could not be saved. Existing games are unchanged."; }
    }
}
