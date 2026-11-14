using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ArcHive.Core;
namespace ArcHive.App;
public partial class SaveBackupsWindow : Window
{
    private readonly Guid gameId;
    private readonly string storage;
    private readonly Action<List<SaveLocation>> save;
    private List<SaveLocation> locations;
    private CancellationTokenSource? busy;
    private SaveRestorePreview? preview;
    private SaveLocation? Location => Locations.SelectedItem as SaveLocation;
    private SaveBackups Service => new(storage, gameId, Location ?? throw new InvalidOperationException("Choose a save folder first."));
    public SaveBackupsWindow(Game game, string storage, Action<List<SaveLocation>> save)
    {
        InitializeComponent(); gameId = game.Id; this.storage = storage; this.save = save;
        locations = JsonSerializer.Deserialize<List<SaveLocation>>(JsonSerializer.Serialize(game.SaveLocations))!;
        Heading.Text = "Save backups · " + game.Title;
        Retention.ItemsSource = Enumerable.Range(1, 100); Locations.ItemsSource = locations;
        Locations.SelectedIndex = locations.Count > 0 ? 0 : -1;
        Closing += (_, e) => { if (busy is not null) { busy.Cancel(); e.Cancel = true; Status.Text = "Waiting for the operation to stop safely. Close again when finished."; } };
    }
    private void Invalidate() { preview = null; RestoreButton.IsEnabled = false; PreviewText.Text = ""; }
    private async Task Run(Func<CancellationToken, Task> action)
    {
        if (busy is not null) return;
        busy = new(); Actions.IsEnabled = Snapshots.IsEnabled = false; Status.Text = "Working on save files…";
        try { await action(busy.Token); }
        catch (Exception ex) { Status.Text = ex is OperationCanceledException ? "Cancelled. Existing saves are unchanged unless restore had already begun its final swap." : ex.Message; }
        finally { busy.Dispose(); busy = null; Actions.IsEnabled = Snapshots.IsEnabled = true; if (Status.Text == "Working on save files…") Status.Text = "Ready. Select a snapshot to preview a restore, or back up now."; }
    }
    private async void SaveLocationChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Snapshots is null || busy is not null) return;
        Invalidate(); Snapshots.ItemsSource = null; FullPath.Text = Location is null ? "" : DisplayPaths.Compact(Location.Folder); FullPath.ToolTip = Location?.Folder; Retention.SelectedItem = Location?.Retention ?? 10;
        if (Location is not null) await Run(async _ => { Snapshots.ItemsSource = await Service.ListAsync(); });
    }
    private void SnapshotChanged(object sender, SelectionChangedEventArgs e) => Invalidate();
    private void AddFolder(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFolderDialog { Title = "Choose this game's save folder" };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            SaveBackups.ValidateLocation(picker.FolderName, storage);
            if (locations.Any(l => Game.SamePath(l.Folder, picker.FolderName))) throw new IOException("This folder is already linked.");
            var added = new SaveLocation { Folder = picker.FolderName }; var updated = locations.Append(added).ToList();
            save(updated); locations = updated; Locations.ItemsSource = locations; Locations.SelectedItem = added;
        }
        catch (Exception ex) { Status.Text = ex.Message; }
    }
    private void SaveRetention(object sender, RoutedEventArgs e)
    {
        if (Location is not { } location || Retention.SelectedItem is not int count) return;
        try
        {
            var updated = locations.Select(l => new SaveLocation { Id = l.Id, Folder = l.Folder, Retention = l.Id == location.Id ? count : l.Retention }).ToList();
            save(updated); locations = updated; Locations.ItemsSource = locations; Locations.SelectedItem = locations.Single(l => l.Id == location.Id);
            Status.Text = "Retention saved. Older regular snapshots are pruned after the next successful backup; safety snapshots stay protected.";
        }
        catch (Exception ex) { Status.Text = ex.Message; }
    }
    private async void Backup(object sender, RoutedEventArgs e) => await Run(async token =>
    {
        Invalidate(); var snapshot = await Service.BackupAsync(token); Snapshots.ItemsSource = await Service.ListAsync();
        Status.Text = "Backup completed: " + snapshot;
    });
    private async void Preview(object sender, RoutedEventArgs e)
    {
        if (Snapshots.SelectedItem is not SaveSnapshot snapshot) { Status.Text = "Select a snapshot first."; return; }
        await Run(async token =>
        {
            Invalidate(); preview = await Service.PreviewAsync(snapshot.Id, token);
            PreviewText.Text = "Destination: " + preview.Destination + "\n\n" + (preview.Changes.Count == 0 ? "No file changes." : string.Join("\n", preview.Changes)) + "\n\nRestore replaces the entire save folder. Current saves will be backed up first.";
            RestoreButton.IsEnabled = true; Status.Text = $"Preview ready: {preview.Changes.Count} changes. Review before restoring.";
        });
    }
    private async void Restore(object sender, RoutedEventArgs e)
    {
        if (preview is not { } plan) return;
        if (MessageBox.Show(this, $"Replace saves in:\n{plan.Destination}\n\n{plan.Changes.Count} changes. Close the game and pause cloud syncing first. Current saves will be backed up and the previous folder retained. Continue?", "Restore saves", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        await Run(async token =>
        {
            Invalidate(); var result = await Service.RestoreAsync(plan, token); Snapshots.ItemsSource = await Service.ListAsync();
            Status.Text = "Restore completed. " + (result.PreviousFolder is null ? "" : "Previous folder retained at: " + result.PreviousFolder);
        });
    }
    private async void Recover(object sender, RoutedEventArgs e) => await Run(async _ => { Invalidate(); Status.Text = await Service.RecoverAsync(); Snapshots.ItemsSource = await Service.ListAsync(); });
    private void Cancel(object sender, RoutedEventArgs e) => busy?.Cancel();
}
