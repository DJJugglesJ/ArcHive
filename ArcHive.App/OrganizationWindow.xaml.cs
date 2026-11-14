using System.Windows;
using System.Windows.Controls;
using ArcHive.Core;

namespace ArcHive.App;

public partial class OrganizationWindow : Window
{
    private readonly Library source;
    private readonly Action<Library> save;
    private BatchPlan? plan;
    private CancellationTokenSource? scan;
    private bool initialized;
    private sealed record ActionChoice(BatchAction Value, string Label);
    public OrganizationWindow(Library source, IEnumerable<Game> visible, Action<Library> save)
    {
        InitializeComponent(); this.source = source; this.save = save;
        Entries.ItemsSource = visible.ToArray();
        Action.ItemsSource = new[] {
            new ActionChoice(BatchAction.Favorite, "Add to favorites"), new(BatchAction.Unfavorite, "Remove from favorites"),
            new(BatchAction.SetStatus, "Set progress status"), new(BatchAction.AddTags, "Add tags"), new(BatchAction.RemoveTags, "Remove tags"),
            new(BatchAction.MarkNsfw, "Mark NSFW"), new(BatchAction.ClearNsfw, "Clear NSFW mark"), new(BatchAction.RemoveEntries, "Remove library entries") };
        Action.DisplayMemberPath = nameof(ActionChoice.Label); Action.SelectedIndex = 0;
        ProgressStatus.ItemsSource = Enum.GetValues<GameProgress>(); ProgressStatus.SelectedIndex = 0;
        initialized = true; UpdateFields();
        Closing += (_, e) => { if (scan is not null) { scan.Cancel(); e.Cancel = true; Summary.Text = "Cancelling scan; close again once it stops."; } };
    }
    private void UpdateFields()
    {
        if (!initialized) return;
        var action = ((ActionChoice)Action.SelectedItem).Value;
        ProgressStatus.Visibility = action == BatchAction.SetStatus ? Visibility.Visible : Visibility.Collapsed;
        TagInput.Visibility = action is BatchAction.AddTags or BatchAction.RemoveTags ? Visibility.Visible : Visibility.Collapsed;
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
        InvalidatePreview(sender, e); Summary.Text = $"{Entries.SelectedItems.Count} of {Entries.Items.Count} shown entries selected.";
    }
    private void SelectAll(object sender, RoutedEventArgs e) => Entries.SelectAll();
    private void ClearSelection(object sender, RoutedEventArgs e) => Entries.UnselectAll();
    private void GroupSelected(object sender, SelectionChangedEventArgs e)
    {
        if (Groups.SelectedItem is not DuplicateGroup group) return;
        Evidence.Text = group.Evidence + "\n\n" + string.Join("\n\n", group.Members.Select(m => m.Title + "\n" + m.Path));
        var ids = group.Members.Select(m => m.GameId).ToHashSet();
        Entries.UnselectAll(); foreach (Game game in Entries.Items) if (ids.Contains(game.Id)) Entries.SelectedItems.Add(game);
        Summary.Text += " Review each entry; title matches may be different versions.";
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
        catch (OperationCanceledException) { Summary.Text = "Scan cancelled. Nothing changed."; }
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
            Summary.Text = $"Review {plan.Changes.Count} entries below. No changes saved yet.";
        }
        catch (Exception ex) { InvalidatePreview(sender, e); Summary.Text = ex.Message; }
    }
    private void Apply(object sender, RoutedEventArgs e)
    {
        if (plan is null || scan is not null) return;
        if (((ActionChoice)Action.SelectedItem).Value == BatchAction.RemoveEntries &&
            MessageBox.Show(this, $"Remove the {plan.Changes.Count} previewed library entries and their saved metadata? Original archives and game folders stay on disk. Rescanning can rediscover them.",
                "Remove library entries", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        try { save(plan.Result); DialogResult = true; }
        catch (Exception ex) { Summary.Text = "Nothing applied. Could not save: " + ex.Message; }
    }
    private void Cancel(object sender, RoutedEventArgs e) => scan?.Cancel();
    private void CloseWindow(object sender, RoutedEventArgs e) => Close();
}
