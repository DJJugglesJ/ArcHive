using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using ArcHive.Core;
using Microsoft.Win32;

namespace ArcHive.App;

public partial class MetadataWindow : Window
{
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false });
    private static readonly VndbMetadataProvider Vndb = new(Client);
    private readonly Game game;
    private readonly CancellationTokenSource cancellation = new();
    private readonly List<(CheckBox Choice, MetadataProposal Proposal)> rows = [];
    public MetadataWindow(Game game)
    {
        InitializeComponent(); this.game = game; QueryBox.Text = game.Title;
        Closed += (_, _) => cancellation.Cancel();
    }
    private MetadataQuery Query() => new(QueryBox.Text, game.ReleaseYear, game.Engine);
    private async void SearchOnline(object sender, RoutedEventArgs e)
    {
        SearchActions.IsEnabled = false; Candidates.ItemsSource = null; Message.Text = "Searching VNDB…";
        try
        {
            var query = Query(); var found = await Vndb.SearchAsync(query, cancellation.Token);
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
        FieldRows.Children.Clear(); rows.Clear(); ApplyButton.IsEnabled = false;
        if (Candidates.SelectedItem is not MetadataMatch match) return;
        foreach (var p in MetadataReconciliation.Preview(game, match.Candidate))
        {
            var choice = new CheckBox { Content = $"Use {p.Field} from {match.Candidate.Provider}/{match.Candidate.Id}", IsChecked = p.Current.Length == 0 };
            FieldRows.Children.Add(choice);
            var origin = p.Provenance is { } source ? $"{source.Provider}/{source.ProviderId} · {source.RetrievedAt:g}" : "existing / local";
            FieldRows.Children.Add(new TextBlock { Text = $"Current ({origin}): {p.Current}\nProposed ({match.Candidate.RetrievedAt:g}): {p.Proposed}", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 18) });
            rows.Add((choice, p));
        }
        ApplyButton.IsEnabled = true;
    }
    private void Apply(object sender, RoutedEventArgs e)
    {
        if (Candidates.SelectedItem is not MetadataMatch match) return;
        try
        {
            var chosen = rows.Where(r => r.Choice.IsChecked == true).Select(r => r.Proposal).ToArray();
            if (chosen.Length == 0) { Message.Text = "Choose at least one field, or cancel to keep everything."; return; }
            MetadataReconciliation.Apply(game, match.Candidate, chosen); DialogResult = true;
        }
        catch (Exception ex) { Message.Text = ex.Message; }
    }
}
