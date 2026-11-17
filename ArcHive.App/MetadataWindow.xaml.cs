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
    private IMetadataProvider? searchProvider;
    private MetadataCandidate? reviewCandidate;
    private CancellationTokenSource? detailRequest;
    private int reviewGeneration, lookupGeneration;
    private CancellationTokenSource? searchRequest;
    private bool focusReviewWhenReady;
    private CheckBox? releaseChoice;
    public MetadataWindow(Game game, MetadataCandidate? initial = null, bool enableF95Zone = false)
    {
        InitializeComponent(); this.game = game; QueryBox.Text = MetadataText.SearchTitle(game);
        F95Choice.IsEnabled = enableF95Zone || initial?.Provider == "f95zone";
        ThreadLink.Text = game.F95Thread?.Url ?? "";
        UpdateProviderHint();
        try { foreach (var plugin in IntegrationSession.Catalog.List().Where(p => p.Enabled && p.Manifest.Capabilities.Contains("metadata"))) ProviderChoice.Items.Add(new ComboBoxItem { Content = plugin.Manifest.Name + " (Plugin)", Tag = plugin.Manifest.Id }); }
        catch (Exception ex) { Message.Text = ex.Message; }
        if (initial is not null) { if (initial.Provider == "f95zone") { ProviderChoice.SelectedIndex = 6; ThreadLink.Text = F95MetadataProvider.Locator(initial.Id); } Show(Query(), [initial]); }
        if (initial is null) Message.Text = "Choose a source and search to review game information.";
        Closed += (_, _) => { reviewGeneration++; lookupGeneration++; detailRequest?.Cancel(); searchRequest?.Cancel(); cancellation.Cancel(); };
        try { ProviderSession.Load(); if (initial is null) ProviderChoice.SelectedIndex = MetadataText.PreferredProvider(ProviderSession.Credentials, enableF95Zone && game.F95Thread is not null); } catch (ProviderException ex) { Message.Text = ex.Message; }
    }
    internal bool HandleController(PadAction action)
    {
        if (action == PadAction.Options)
        {
            var menu = new ContextMenu { PlacementTarget = this, Placement = System.Windows.Controls.Primitives.PlacementMode.Center };
            void Add(string label, Action action, bool enabled = true)
            { var item = new MenuItem { Header=label, IsEnabled=enabled }; item.Click += (_,_)=> { menu.IsOpen=false; action(); }; menu.Items.Add(item); }
            Add("Apply Reviewed Fields", () => Apply(this,new()), reviewCandidate is not null && ApplyButton.IsEnabled);
            Add("Back to Matches", () => Candidates.Focus(), Candidates.Items.Count>0);
            Add("Choose Source", () => ProviderChoice.Focus(), searchRequest is null);
            Add("Stop Search", () => searchRequest?.Cancel(), searchRequest is not null);
            Add("Return to Review", () => { focusReviewWhenReady=true; FocusReview(); });
            menu.Opened += (_,_) => menu.Dispatcher.BeginInvoke(new Action(()=>menu.Items.OfType<MenuItem>().FirstOrDefault(i=>i.IsEnabled)?.Focus()));
            menu.IsOpen=true; return true;
        }
        if (action == PadAction.Cancel && searchRequest is not null) { searchRequest.Cancel(); return true; }
        if (action == PadAction.Accept && Candidates.IsKeyboardFocusWithin && Candidates.SelectedItem is MetadataMatch)
        {
            focusReviewWhenReady = true; FocusReview(); return true;
        }
        return false;
    }
    private void FocusReview()
    {
        if (!focusReviewWhenReady || reviewCandidate is null) return;
        focusReviewWhenReady = false;
        var target = FieldRows.Children.OfType<Control>().FirstOrDefault(c => c.IsEnabled && c.Focusable);
        if (target is not null) { target.Focus(); target.BringIntoView(); }
    }
    private void CancelSearch(object sender, RoutedEventArgs e) => searchRequest?.Cancel();
    private void ProviderChanged(object sender, SelectionChangedEventArgs e)
    { if (Candidates is null) return; searchRequest?.Cancel(); lookupGeneration++; Candidates.ItemsSource = null; searchProvider = null; UpdateProviderHint(); }
    private void SearchInputChanged(object sender, TextChangedEventArgs e)
    { if (Candidates is null) return; searchRequest?.Cancel(); lookupGeneration++; Candidates.ItemsSource = null; if (Message is not null) Message.Text = "Search again to use the changed query or thread link."; }
    private void UpdateProviderHint()
    {
        if (ProviderHint is null || ThreadInput is null) return;
        var f95 = ProviderChoice.SelectedIndex == 6; ThreadInput.Visibility = f95 ? Visibility.Visible : Visibility.Collapsed;
        ProviderHint.Text = ProviderChoice.SelectedIndex switch
        {
            0 => "VNDB searches visual novels and returns descriptions and non-spoiler tags. Other game types may not be listed.",
            1 => "Steam requires a Web API key for catalog search. Selecting a match loads its public store description and genres; store details may be unavailable for some games.",
            2 => "IGDB needs a Twitch client ID and secret. Searches descriptions, genres, themes and keywords.",
            3 or 4 => "itch.io searches only your owned games or projects, not the public catalog. Its account API may not supply tags.",
            5 => "RAWG needs an API key. Selecting a match loads the full description and tags.",
            6 => "Paste an F95Zone thread URL or numeric thread ID below. Public thread descriptions, tags, engine and version evidence are reviewed here. Browser logins are not shared with ArcHive.",
            _ => "Plugin results are reviewed before applying any fields."
        };
    }
    private void ConfigureProviders(object sender, RoutedEventArgs e)
    { if (new ProviderSettingsWindow { Owner = this }.ShowDialog() == true) { lookupGeneration++; searchProvider = null; Candidates.ItemsSource = null; Message.Text = "Provider settings updated. Search when ready."; } }
    private void OpenSource(object sender, System.Windows.Navigation.RequestNavigateEventArgs e) => MetadataSourceLinks.Open(e);
    private MetadataQuery Query() => new(QueryBox.Text, game.ReleaseYear, game.Engine);
    private async void SearchOnline(object sender, RoutedEventArgs e)
    {
        if (searchRequest is not null) return;
        using var request = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token); searchRequest = request;
        var generation = ++lookupGeneration;
        StopSearch.Visibility = Visibility.Visible; StopSearch.Focus();
        SearchActions.IsEnabled = false; Candidates.ItemsSource = null; Message.Text = "Searching selected provider…";
        try
        {
            var query = Query();
            IMetadataProvider provider = ProviderChoice.SelectedItem is ComboBoxItem { Tag: string pluginId } ? new PluginMetadataProvider(IntegrationSession.Catalog, pluginId) : ProviderSession.Get(ProviderChoice.SelectedIndex);
            searchProvider = provider;
            var lookup = provider.Id == "f95zone" ? new MetadataQuery(ThreadLink.Text) : query;
            var found = await Task.Run(() => provider.SearchAsync(lookup, request.Token), request.Token);
            request.Token.ThrowIfCancellationRequested(); if (generation != lookupGeneration) return; Show(query, found);
            if (found.Count > 0) Candidates.Focus();
        }
        catch (OperationCanceledException) { if (generation == lookupGeneration && !cancellation.IsCancellationRequested) Message.Text = "Search stopped. Choose a source or search again."; }
        catch (Exception ex) { if (generation == lookupGeneration && !cancellation.IsCancellationRequested) Message.Text = "Couldn't retrieve game information. No changes were made. " + ex.Message; }
        finally { searchRequest = null; SearchActions.IsEnabled = true; StopSearch.Visibility = Visibility.Collapsed; if (!cancellation.IsCancellationRequested && Candidates.Items.Count == 0) SearchButton.Focus(); }
    }
    private void ImportOffline(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Metadata JSON|*.json" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > 2_000_000) throw new InvalidDataException("Import exceeds 2 MB.");
            var provider = OfflineMetadataProvider.Parse(File.ReadAllText(dialog.FileName));
            searchProvider = null; Show(Query(), provider.SearchAsync(Query(), cancellation.Token).GetAwaiter().GetResult());
        }
        catch (Exception ex) { Message.Text = "Import failed. " + ex.Message; }
    }
    private void Show(MetadataQuery query, IReadOnlyList<MetadataCandidate> found)
    {
        Candidates.ItemsSource = MetadataMatching.Rank(query, found);
        Message.Text = found.Count == 0 ? "No matches found. Try another search term or provider." : "Select a candidate to review its fields. No match is selected automatically.";
    }
    private async void SelectCandidate(object sender, SelectionChangedEventArgs e)
    {
        focusReviewWhenReady = false;
        var generation = ++reviewGeneration; detailRequest?.Cancel(); detailRequest = null;
        FieldRows.Children.Clear(); rows.Clear(); artworkChoices.Clear(); releaseChoice = null; reviewCandidate = null; ApplyButton.IsEnabled = false;
        if (Candidates.SelectedItem is not MetadataMatch match) return;
        var candidate = match.Candidate;
        if (searchProvider is not null && ProviderSession.Details(searchProvider) is { } details)
        {
            using var request = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token); detailRequest = request;
            Message.Text = "Loading details for the selected match…";
            try
            {
                var expanded = await details.GetDetailsAsync(candidate, request.Token);
                request.Token.ThrowIfCancellationRequested();
                if (generation != reviewGeneration) return;
                MetadataValidation.Validate(expanded);
                if (expanded.Provider != candidate.Provider || expanded.Id != candidate.Id) throw new InvalidDataException("Details do not belong to the selected match.");
                candidate = expanded;
                Message.Text = "Details loaded. Review the fields, then Apply Changes.";
            }
            catch (OperationCanceledException) { if (generation != reviewGeneration || cancellation.IsCancellationRequested) return; Message.Text = "Detail lookup timed out. Available search fields are shown; reselect the match to retry."; }
            catch (Exception ex) { if (generation != reviewGeneration) return; Message.Text = "Full details could not be loaded. Available search fields remain usable. " + ex.Message; }
            finally { if (generation == reviewGeneration) detailRequest = null; }
        }
        if (generation != reviewGeneration || cancellation.IsCancellationRequested) return;
        reviewCandidate = candidate; RenderCandidate(candidate); FocusReview();
    }
    private void RenderCandidate(MetadataCandidate candidate)
    {
        foreach (var p in MetadataReconciliation.Preview(game, candidate))
        {
            var choice = new CheckBox { Content = $"Use {p.Field} from {candidate.Provider}/{candidate.Id}", IsChecked = p.Current.Length == 0 };
            FieldRows.Children.Add(choice);
            var origin = p.Provenance is { } source ? $"{source.Provider}/{source.ProviderId} · {source.RetrievedAt:g}" : "existing / local";
            FieldRows.Children.Add(new TextBlock { Text = $"Current ({origin}): {p.Current}\nProposed ({candidate.RetrievedAt:g}): {p.Proposed}", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 18) });
            rows.Add((choice, p));
        }
        if (candidate.Artwork.Length > 0)
        {
            var choiceButtons = new List<(Button Button, ArtworkReference Art, ArtworkKind Role)>();
            FieldRows.Children.Add(new TextBlock { Text = "Artwork — preview downloads an image; choose roles before Apply.", TextWrapping = TextWrapping.Wrap, FontSize = 16 });
            foreach (var art in candidate.Artwork)
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
        if (candidate.ObservedRelease is { } release && !string.IsNullOrWhiteSpace(release.Version))
        {
            releaseChoice = new CheckBox { Content = "Use Observed Release", IsChecked = false }; FieldRows.Children.Add(releaseChoice);
            FieldRows.Children.Add(new TextBlock { Text = $"Current latest release: {game.LatestRelease.Version} {game.LatestRelease.Channel}\nProposed: {release.Version} {release.Channel}\nThis updates latest-known release evidence, not your installed version. Different channels remain incomparable.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,18) });
        }
        if (rows.Count == 1 && candidate.Fields.Count == 0) Message.Text = "This result contains a title only. No description or tags were returned. Try another source or check its connection settings.";
        ApplyButton.IsEnabled = true;
    }
    private void Apply(object sender, RoutedEventArgs e)
    {
        if (reviewCandidate is not { } candidate || Candidates.SelectedItem is not MetadataMatch match || match.Candidate.Id != candidate.Id || match.Candidate.Provider != candidate.Provider) return;
        try
        {
            var chosen = rows.Where(r => r.Choice.IsChecked == true).Select(r => r.Proposal).ToArray();
            if (chosen.Length == 0 && artworkChoices.Count == 0 && releaseChoice?.IsChecked != true) { Message.Text = "Choose a field or artwork, or cancel to keep everything."; return; }
            if (candidate.Provider == "f95zone") F95MetadataProvider.Locator(candidate.Id);
            var artCopy = System.Text.Json.JsonSerializer.Deserialize<GameArtwork>(System.Text.Json.JsonSerializer.Serialize(game.Artwork))!;
            var staging = new Game { Artwork = artCopy };
            foreach (var choice in artworkChoices) ProviderArtwork.Select(staging, choice.Art, choice.Role);
            MetadataReconciliation.Apply(game, candidate, chosen); game.Artwork = artCopy;
            if (candidate.Provider == "f95zone") F95ThreadSource.ApplyEvidence(game, candidate, releaseChoice?.IsChecked == true);
            else if (releaseChoice?.IsChecked == true && candidate.ObservedRelease is { } release)
            {
                game.LatestRelease = release with { }; game.LatestReleaseSource = candidate.Provider + "/" + candidate.Id;
                game.LatestReleaseCheckedAt = candidate.RetrievedAt; VersionTracking.Record(game, game.LatestRelease, game.LatestReleaseSource);
            }
            DialogResult = true;
        }
        catch (Exception ex) { Message.Text = ex.Message; }
    }
}
