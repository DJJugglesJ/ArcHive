using System.Windows;
using System.Windows.Controls;
using ArcHive.Core;
using Microsoft.Win32;
namespace ArcHive.App;
public partial class ArchiveQueueWindow : Window
{
    private readonly ExtractionQueue queue;
    private readonly Game? game;
    private readonly GameArchive? archive;
    private readonly Func<ExtractionJob, Task> link;
    private CancellationTokenSource? operation;
    public ArchiveQueueWindow(ExtractionQueue queue, Game? game, GameArchive? archive, Func<ExtractionJob, Task> link)
    {
        InitializeComponent(); this.queue = queue; this.game = game; this.archive = archive; this.link = link;
        SourcePath.Text = archive?.Path ?? "Select a game's archive and choose Preview / queue to add an extraction.";
        PreviewButton.IsEnabled = archive is not null;
        RefreshJobs();
        Closing += (_, e) => { if (operation is not null) { operation.Cancel(); e.Cancel = true; Status.Text = "Stopping; close again when the current read finishes."; } };
    }
    private void RefreshJobs()
    {
        var id = (Jobs.SelectedItem as ExtractionJob)?.Id;
        Jobs.ItemsSource = queue.Jobs.ToArray(); Jobs.SelectedItem = queue.Jobs.FirstOrDefault(j => j.Id == id);
    }
    private async Task Work(Func<CancellationToken, Task> action)
    {
        if (operation is not null) return;
        operation = new(); PreviewControls.IsEnabled = QueueControls.IsEnabled = false; StopButton.IsEnabled = true;
        try { await action(operation.Token); }
        catch (OperationCanceledException) { Status.Text = "Cancelled. Original files retained."; }
        catch (Exception ex) { Status.Text = ex.Message; }
        finally { operation.Dispose(); operation = null; PreviewControls.IsEnabled = QueueControls.IsEnabled = true; StopButton.IsEnabled = false; RefreshJobs(); }
    }
    private async void Preview(object sender, RoutedEventArgs e)
    {
        if (archive is null) return;
        EnqueueButton.IsEnabled = false; Contents.ItemsSource = null;
        await Work(async token =>
        {
            var result = await ArchiveInspection.ReadAsync(archive.Path, token);
            Contents.ItemsSource = result.Entries;
            PreviewSummary.Text = $"{result.Entries.Count:N0} entries · {result.ArchiveBytes:N0} archive bytes · {result.ExpandedBytes:N0} declared output bytes";
            EnqueueButton.IsEnabled = result.CanExtract;
            Status.Text = result.CanExtract ? "Preview ready. Extraction rechecks the archive; preview does not verify compressed payload integrity." : "Unsupported entries found. Review the Issue column.";
        });
    }
    private void Enqueue(object sender, RoutedEventArgs e)
    {
        if (game is null || archive is null || !EnqueueButton.IsEnabled) return;
        var picker = new OpenFolderDialog { Title = "Choose extraction parent (each job gets a new folder)" };
        if (picker.ShowDialog(this) != true) return;
        Try(() => { queue.Enqueue(game, archive, picker.FolderName); Status.Text = "Queued. Run when ready, or close and add more archives."; });
    }
    private async void Run(object sender, RoutedEventArgs e) => await Work(async token =>
    {
        IArchiveExtractor extractor =
#if MULTI_ARCHIVE
            new MultiArchiveExtractor();
#else
            new ZipExtractor();
#endif
        var progress = new Progress<string>(message => { if (operation is not null) { Status.Text = message; RefreshJobs(); } });
        await queue.RunAsync(extractor, link, progress, token);
        Status.Text = token.IsCancellationRequested ? "Queue stopped. Later jobs remain pending." : "Queue pass finished. Review states and errors; Extracted jobs can retry linking without extracting again.";
    });
    private void Retry(object sender, RoutedEventArgs e) => Try(() => { if (Jobs.SelectedItem is ExtractionJob job) queue.Retry(job.Id); });
    private void CancelPending(object sender, RoutedEventArgs e) => Try(() => { if (Jobs.SelectedItem is ExtractionJob job) queue.CancelPending(job.Id); });
    private void Try(Action action) { try { action(); RefreshJobs(); } catch (Exception ex) { Status.Text = ex.Message; } }
    private void SelectedJob(object sender, SelectionChangedEventArgs e)
    {
        if (JobDetails is null) return;
        JobDetails.Text = Jobs.SelectedItem is ExtractionJob job ? $"Archive: {job.Archive}\nOutput: {job.Output ?? "Not recorded"}\n{job.Error}\nAttempt folders (retained):\n{string.Join("\n", job.Attempts)}" : "";
    }
    private void Stop(object sender, RoutedEventArgs e) => operation?.Cancel();
    private void CloseWindow(object sender, RoutedEventArgs e) => Close();
}
