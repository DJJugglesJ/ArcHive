using System.Windows;
using System.Windows.Controls;
using ArcHive.Core;

namespace ArcHive.App;

public partial class OrganizationWindow : Window
{
    private readonly Library source;
    private readonly Action<Library> save;
    private readonly Action<Game[]>? advanced;
    private BatchPlan? plan;
    private CancellationTokenSource? scan;
    private bool initialized;
    private sealed record ActionChoice(BatchAction Value, string Label);
    public OrganizationWindow(Library source, IEnumerable<Game> visible, Action<Library> save, Action<Game[]>? advanced = null)
    {
        InitializeComponent(); this.source = source; this.save = save; this.advanced = advanced;
        Entries.ItemsSource = visible.ToArray();
        Action.ItemsSource = new[] {
            new ActionChoice(BatchAction.Favorite, "Add to Favorites"), new(BatchAction.Unfavorite, "Remove from Favorites"),
            new(BatchAction.SetStatus, "Set Play Status"), new(BatchAction.AddTags, "Add Tags"), new(BatchAction.RemoveTags, "Remove Tags"),
            new(BatchAction.MarkNsfw, "Mark NSFW"), new(BatchAction.ClearNsfw, "Remove NSFW Tag"), new(BatchAction.RemoveEntries, "Remove from Library"), new(BatchAction.AddCollection, "Add to Collections"), new(BatchAction.RemoveCollection, "Remove from Collections"), new(BatchAction.MarkPlayed, "Mark Played"), new(BatchAction.MarkUnplayed, "Mark Unplayed") };
        Action.DisplayMemberPath = nameof(ActionChoice.Label); Action.SelectedIndex = 0;
        ProgressStatus.ItemsSource = Enum.GetValues<GameProgress>(); ProgressStatus.SelectedIndex = 0;
        initialized = true; UpdateFields();
        Closing += (_, e) => { if (scan is not null) { scan.Cancel(); e.Cancel = true; Summary.Text = "Canceling scan; close again once it stops."; } };
    }
    private void UpdateFields()
    {
        if (!initialized) return;
        var action = ((ActionChoice)Action.SelectedItem).Value;
        ProgressStatus.Visibility = action == BatchAction.SetStatus ? Visibility.Visible : Visibility.Collapsed;
        TagInput.Visibility = action is BatchAction.AddTags or BatchAction.RemoveTags or BatchAction.AddCollection or BatchAction.RemoveCollection ? Visibility.Visible : Visibility.Collapsed;
    }
    private void ActionChanged(object sender, SelectionChangedEventArgs e) { UpdateFields(); InvalidatePreview(sender, e); }
    private void InvalidatePreview(object sender, RoutedEventArgs e)
    {
        if (!initialized) return;
        plan = null; Changes.ItemsSource = null; ApplyButton.IsEnabled = false;
    }
    private void SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!initialized) return;
        Evidence.Text = string.Join("\n\n", Entries.SelectedItems.Cast<Game>().Select(g => g.Title + "\n" + g.CompactSourceSummary));
        InvalidatePreview(sender, e); Summary.Text = $"{Entries.SelectedItems.Count} of {Entries.Items.Count} shown games selected.";
    }
    private void SelectAll(object sender, RoutedEventArgs e) => Entries.SelectAll();
    private void ClearSelection(object sender, RoutedEventArgs e) => Entries.UnselectAll();
    private void GroupSelected(object sender, SelectionChangedEventArgs e)
    {
        if (Groups.SelectedItem is not DuplicateGroup group) return;
        var ids = group.Members.Select(m => m.GameId).ToHashSet();
        Entries.UnselectAll(); foreach (Game game in Entries.Items) if (ids.Contains(game.Id)) Entries.SelectedItems.Add(game);
        Evidence.Text = group.Evidence + "\n\n" + string.Join("\n\n", group.Members.Select(m => m.Title + "\n" + DisplayPaths.Compact(m.Path)));
        Summary.Text += " Review each game; title matches may be different versions.";
    }
    private async void FindDuplicates(object sender, RoutedEventArgs e)
    {
        if (scan is not null) return;
        InvalidatePreview(sender, e); Groups.ItemsSource = null; Evidence.Clear();
        scan = new(); ScanControls.IsEnabled = ActionControls.IsEnabled = Entries.IsEnabled = false; CancelScan.IsEnabled = true;
        var candidates = Entries.Items.Cast<Game>().ToArray(); var hash = HashArchives.IsChecked == true;
        var progress = new Progress<string>(s => { if (scan is not null) Summary.Text = s; });
        try
        {
            var report = await Task.Run(() => DuplicateDetection.ScanAsync(candidates, hash, progress, scan.Token));
            Groups.ItemsSource = report.Groups; Evidence.Text = string.Join("\n", report.Warnings);
            Summary.Text = $"{report.Groups.Count} groups; {report.Warnings.Count} skipped locations. Select a group to review paths.";
        }
        catch (OperationCanceledException) { Summary.Text = "Scan canceled. Nothing changed."; }
        catch (Exception ex) { Summary.Text = "Scan failed: " + ex.Message; }
        finally { scan.Dispose(); scan = null; ScanControls.IsEnabled = ActionControls.IsEnabled = Entries.IsEnabled = true; CancelScan.IsEnabled = false; }
    }
    private void Preview(object sender, RoutedEventArgs e)
    {
        try
        {
            plan = BatchOperations.Preview(source, Entries.SelectedItems.Cast<Game>().Select(g => g.Id),
                new(((ActionChoice)Action.SelectedItem).Value, (GameProgress)ProgressStatus.SelectedItem, TagInput.Text));
            Changes.ItemsSource = plan.Changes; ApplyButton.IsEnabled = true;
            Summary.Text = $"Review {plan.Changes.Count} games below. No changes saved yet.";
        }
        catch (Exception ex) { InvalidatePreview(sender, e); Summary.Text = ex.Message; }
    }
    private void Apply(object sender, RoutedEventArgs e)
    {
        if (plan is null || scan is not null) return;
        if (((ActionChoice)Action.SelectedItem).Value == BatchAction.RemoveEntries &&
            !ChoiceDialog.Confirm(this, "Remove from Library", UiText.RemoveGames(plan.Changes.Count), "Remove from Library")) return;
        try { save(plan.Result); DialogResult = true; }
        catch (Exception ex) { Summary.Text = "Nothing applied. Could not save: " + ex.Message; }
    }
    private void Advanced(object sender, RoutedEventArgs e)
    {
        if (scan is not null || advanced is null) return;
        var games = Entries.SelectedItems.Cast<Game>().ToArray();
        if (games.Length == 0) { Summary.Text = "Select games first."; return; }
        advanced(games); DialogResult = true;
    }
    private void Cancel(object sender, RoutedEventArgs e) => scan?.Cancel();
    private void CloseWindow(object sender, RoutedEventArgs e) => Close();
}
