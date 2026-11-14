using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using ArcHive.Core;
using Microsoft.Win32;

namespace ArcHive.App;

public partial class MetadataWindow : Window
{
    private readonly Game game;
    private readonly CancellationTokenSource cancellation = new();
    private readonly List<(CheckBox Choice, MetadataProposal Proposal)> rows = [];
    private readonly List<(ArtworkReference Art, ArtworkKind Role)> artworkChoices = [];
    public MetadataWindow(Game game, MetadataCandidate? initial = null)
    {
        InitializeComponent(); this.game = game; QueryBox.Text = game.Title;
        if (initial is not null) Show(Query(), [initial]);
        Closed += (_, _) => cancellation.Cancel();
        try { ProviderSession.Load(); } catch (ProviderException ex) { Message.Text = ex.Message; }
    }
    private void ProviderChanged(object sender, SelectionChangedEventArgs e)
    { if (Candidates is not null) Candidates.ItemsSource = null; }
    private void ConfigureProviders(object sender, RoutedEventArgs e)
    { if (new ProviderSettingsWindow { Owner = this }.ShowDialog() == true) { Candidates.ItemsSource = null; Message.Text = "Provider settings updated. Search when ready."; } }
    private void OpenSource(object sender, System.Windows.Navigation.RequestNavigateEventArgs e) => MetadataSourceLinks.Open(e);
    private MetadataQuery Query() => new(QueryBox.Text, game.ReleaseYear, game.Engine);
    private async void SearchOnline(object sender, RoutedEventArgs e)
    {
        SearchActions.IsEnabled = false; Candidates.ItemsSource = null; Message.Text = "Searching selected provider…";
        try
        {
            var query = Query(); var found = await ProviderSession.Get(ProviderChoice.SelectedIndex).SearchAsync(query, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested(); Show(query, found);
        }
        catch (OperationCanceledException) { Message.Text = "Search canceled or timed out. You can retry."; }
        catch (Exception ex) { Message.Text = "Lookup failed. Your library is unchanged. " + ex.Message; }
        finally { SearchActions.IsEnabled = true; }
    }
    private void ImportOffline(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Metadata JSON|*.json" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > 2_000_000) throw new InvalidDataException("Import exceeds 2 MB.");
            var provider = OfflineMetadataProvider.Parse(File.ReadAllText(dialog.FileName));
            Show(Query(), provider.SearchAsync(Query(), cancellation.Token).GetAwaiter().GetResult());
        }
        catch (Exception ex) { Message.Text = "Import failed. " + ex.Message; }
    }
    private void Show(MetadataQuery query, IReadOnlyList<MetadataCandidate> found)
    {
        Candidates.ItemsSource = MetadataMatching.Rank(query, found);
        Message.Text = found.Count == 0 ? "No candidates found. Try another title or import offline metadata." : "Select a candidate to review its fields. No match is selected automatically.";
    }
    private void SelectCandidate(object sender, SelectionChangedEventArgs e)
    {
        FieldRows.Children.Clear(); rows.Clear(); artworkChoices.Clear(); ApplyButton.IsEnabled = false;
        if (Candidates.SelectedItem is not MetadataMatch match) return;
        foreach (var p in MetadataReconciliation.Preview(game, match.Candidate))
        {
            var choice = new CheckBox { Content = $"Use {p.Field} from {match.Candidate.Provider}/{match.Candidate.Id}", IsChecked = p.Current.Length == 0 };
            FieldRows.Children.Add(choice);
            var origin = p.Provenance is { } source ? $"{source.Provider}/{source.ProviderId} · {source.RetrievedAt:g}" : "existing / local";
            FieldRows.Children.Add(new TextBlock { Text = $"Current ({origin}): {p.Current}\nProposed ({match.Candidate.RetrievedAt:g}): {p.Proposed}", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 18) });
            rows.Add((choice, p));
        }
        if (match.Candidate.Artwork.Length > 0)
        {
            var choiceButtons = new List<(Button Button, ArtworkReference Art, ArtworkKind Role)>();
            FieldRows.Children.Add(new TextBlock { Text = "Artwork — preview downloads an image; choose roles before Apply.", TextWrapping = TextWrapping.Wrap, FontSize = 16 });
            foreach (var art in match.Candidate.Artwork)
            {
                var image = new Artwork { Width = 240, Height = 130, Stretch = System.Windows.Media.Stretch.Uniform };
                FieldRows.Children.Add(new TextBlock { Text = $"{art.Kind} · {art.Provider}/{art.ProviderId} · {art.RetrievedAt:g}", TextWrapping = TextWrapping.Wrap });
                FieldRows.Children.Add(image);
                var actions = new WrapPanel(); var preview = new Button { Content = "Preview" };
                preview.Click += (_, _) => image.Reference = art; actions.Children.Add(preview);
                foreach (var role in Enum.GetValues<ArtworkKind>())
                {
                    var choose = new Button { Content = "Use as " + role };
                    choose.Click += (_, _) =>
                    {
                        if (artworkChoices.Contains((art, role))) artworkChoices.Remove((art, role));
                        else
                        {
                            if (role != ArtworkKind.Screenshot) artworkChoices.RemoveAll(c => c.Role == role);
                            artworkChoices.Add((art, role));
                        }
                        foreach (var button in choiceButtons) button.Button.Content = (artworkChoices.Contains((button.Art, button.Role)) ? "Selected: " : "Use as ") + button.Role;
                        Message.Text = $"Selected {artworkChoices.Count} artwork choices. Apply to save; Cancel discards them.";
                    };
                    choiceButtons.Add((choose, art, role)); actions.Children.Add(choose);
                }
                FieldRows.Children.Add(actions);
            }
        }
        else FieldRows.Children.Add(new TextBlock { Text = "This source returned no supported artwork. Existing images will remain.", TextWrapping = TextWrapping.Wrap });
        ApplyButton.IsEnabled = true;
    }
    private void Apply(object sender, RoutedEventArgs e)
    {
        if (Candidates.SelectedItem is not MetadataMatch match) return;
        try
        {
            var chosen = rows.Where(r => r.Choice.IsChecked == true).Select(r => r.Proposal).ToArray();
            if (chosen.Length == 0 && artworkChoices.Count == 0) { Message.Text = "Choose a field or artwork, or cancel to keep everything."; return; }
            var artCopy = System.Text.Json.JsonSerializer.Deserialize<GameArtwork>(System.Text.Json.JsonSerializer.Serialize(game.Artwork))!;
            var staging = new Game { Artwork = artCopy };
            foreach (var choice in artworkChoices) ProviderArtwork.Select(staging, choice.Art, choice.Role);
            MetadataReconciliation.Apply(game, match.Candidate, chosen); game.Artwork = artCopy; DialogResult = true;
        }
        catch (Exception ex) { Message.Text = ex.Message; }
    }
}
