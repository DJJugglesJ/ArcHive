using System.IO;
using System.Windows;
using System.Windows.Controls;
using ArcHive.Core;
using Microsoft.Win32;
namespace ArcHive.App;
public sealed class BatchToolsWindow : ToolWindow
{
    private readonly Library source;
    private readonly Game[] selection;
    private readonly Action<Library> save;
    private readonly string journal;
    private readonly ExtractionQueue queue;
    private readonly Func<Library> readPersisted;
    private readonly TextBox preview = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, MinHeight = 150, MaxHeight = 240, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private Action? apply;
    private readonly Button applyButton;
    private bool working;
    public BatchToolsWindow(Library source, Game[] selection, Action<Library> save, ExtractionQueue queue, string journal, Func<Library> readPersisted) : base("Batch Tools")
    {
        this.source = source; this.selection = selection; this.save = save; this.queue = queue; this.journal = journal; this.readPersisted = readPersisted;
        Label($"{selection.Length} games selected. Review changes before applying. Original files are retained by merging and extraction; move tools change paths only after confirmation.");
        var primary = new ComboBox { ItemsSource = selection, DisplayMemberPath = nameof(Game.Title), SelectedIndex = 0 }; Body.Children.Add(primary);
        primary.SelectionChanged += (_, _) => Invalidate();
        ActionButton("Preview Duplicate Merge", () => { Invalidate(); CheckQueue(); var p = DuplicateMerge.Preview(source, selection.Select(g => g.Id), ((Game)primary.SelectedItem).Id); preview.Text = string.Join("\n\n", p.Summary); Ready(() => { save(DuplicateMerge.Apply(source, p)); DialogResult = true; }); });
        ActionButton("Preview File Renames…", () => BeginFiles(null, true));
        ActionButton("Preview Relocation…", () => { var picker = new OpenFolderDialog { Title = "Choose Destination on the Same Drive" }; if (picker.ShowDialog(this) == true) BeginFiles(picker.FolderName, false); });
        ActionButton("Recover Interrupted File Changes", () => { Invalidate(); StatusText.Text = LibraryFileMoves.Recover(readPersisted(), journal); });
        ActionButton("Preview Batch Extraction…", BeginExtraction);
        ActionButton("Scan Selected Game Files…", BeginDetection);
        Label("Rename uses the reviewed canonical title, or the current display title. Relocation retains filenames. Shared, nested, cross-drive and conflicting paths are rejected. Folders containing configured saves are excluded to keep backup paths valid.");
        Body.Children.Add(preview); applyButton = ActionButton("Apply Previewed Changes", () => { if (apply is null) return; if (!ChoiceDialog.Confirm(this, "Apply Batch Changes", "Apply exactly the changes in this preview? Close affected games and other applications that use these files first.", "Apply Changes")) return; apply(); }); applyButton.IsEnabled = false;
        Body.Children.Add(StatusText); ActionButton("Close", Close);
        Closing += (_, e) => { if (working) { e.Cancel = true; StatusText.Text = "Wait for verification to finish."; } };
    }
    private void Invalidate() { apply = null; if (applyButton is not null) applyButton.IsEnabled = false; preview.Clear(); }
    private void Ready(Action action) { apply = action; applyButton.IsEnabled = true; }
    private void CheckQueue()
    { if (File.Exists(journal)) throw new InvalidOperationException("Recover the pending file operation before further changes."); if (queue.IsRunning || queue.Jobs.Any(j => j.State is ExtractionState.Pending or ExtractionState.Running or ExtractionState.Interrupted or ExtractionState.Extracted)) throw new InvalidOperationException("Finish or cancel pending extraction jobs before moving or merging their library records."); }
    private async void BeginFiles(string? folder, bool rename)
    {
        if (working) return; Invalidate(); working = true; Body.IsEnabled = false;
        try { CheckQueue(); var p = await Task.Run(() => LibraryFileMoves.Preview(source, selection.Select(g => g.Id), folder, rename)); preview.Text = string.Join("\n\n", p.Moves.Select(m => m.Source + "\n→ " + m.Destination)); Ready(() => ApplyFiles(p)); }
        catch (Exception ex) { StatusText.Text = ex.Message; }
        finally { working = false; Body.IsEnabled = true; }
    }
    private async void ApplyFiles(FileMovePlan plan)
    {
        if (working) return; working = true; Body.IsEnabled = false;
        try
        {
            CheckQueue();
            // Persistence is dispatched to the UI owner only after file verification, keeping its live model coherent.
            await Task.Run(() => LibraryFileMoves.Apply(source, plan, journal, updated => Dispatcher.Invoke(() => save(updated))));
            working = false; DialogResult = true;
        }
        catch (Exception ex)
        {
            Invalidate(); StatusText.Text = ex.Message;
            try { StatusText.Text += " " + await Task.Run(() => LibraryFileMoves.Recover(readPersisted(), journal)); }
            catch (Exception recovery) { StatusText.Text += " Recovery needs attention: " + recovery.Message + " Close ArcHive and retain the journal for manual recovery."; }
        }
        finally { working = false; Body.IsEnabled = true; }
    }
    private async void BeginExtraction()
    {
        if (working) return; Invalidate();
        var picker = new OpenFolderDialog { Title = "Choose Extraction Parent Folder" }; if (picker.ShowDialog(this) != true) return;
        working = true; Body.IsEnabled = false;
        try
        {
            var items = selection.SelectMany(g => g.Archives.Select(a => (Game: g, Archive: a, Length: new FileInfo(a.Path).Length, Write: File.GetLastWriteTimeUtc(a.Path)))).ToArray();
            if (items.Length == 0) throw new InvalidOperationException("No archives in the selection.");
            var lines = new List<string>();
            foreach (var item in items)
            { var report = await ArchiveInspection.ReadAsync(item.Archive.Path, default); if (!report.CanExtract) throw new IOException("Archive requires attention: " + item.Archive.Path); lines.Add(item.Game.Title + "\n" + item.Archive.Path + "\n→ " + picker.FolderName); }
            preview.Text = string.Join("\n\n", lines);
            Ready(() => { foreach (var item in items) if (new FileInfo(item.Archive.Path).Length != item.Length || File.GetLastWriteTimeUtc(item.Archive.Path) != item.Write) throw new IOException("Archive changed. Preview again."); queue.EnqueueBatch(items.Select(x => (x.Game, x.Archive)), picker.FolderName); StatusText.Text = "Archives queued. Open Extraction Queue to run them."; Invalidate(); });
        }
        catch (Exception ex) { StatusText.Text = ex.Message; }
        finally { working = false; Body.IsEnabled = true; }
    }
    private void BeginDetection()
    {
        if (working) return; Invalidate(); working = true; Body.IsEnabled = false;
        try
        {
            var updated = source; var lines = new List<string>();
            foreach (var game in selection.Where(g => g.Installations.Count > 0))
            {
                var current = updated.Games.Single(g => g.Id == game.Id);
                var choice = new DetectionWindow(updated, current, result => updated = result) { Owner = this };
                if (choice.ShowDialog() == true) lines.Add(game.Title + ": reviewed file suggestions added to preview.");
            }
            if (lines.Count == 0) { StatusText.Text = "No suggestions selected. Nothing changed."; return; }
            preview.Text = string.Join("\n", lines); var result = updated; Ready(() => { save(result); DialogResult = true; });
        }
        catch (Exception ex) { StatusText.Text = ex.Message; }
        finally { working = false; Body.IsEnabled = true; }
    }
}
