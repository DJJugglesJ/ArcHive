using System.Windows;
using System.Windows.Controls;
using ArcHive.Core;
namespace ArcHive.App;
public partial class DetectionWindow : Window
{
    private readonly Library source;
    private readonly Game game;
    private readonly Action<Library> save;
    private DetectionReport? report;
    private CancellationTokenSource? scan;
    public DetectionWindow(Library source, Game game, Action<Library> save)
    {
        InitializeComponent(); this.source = source; this.game = game; this.save = save;
        Current.Text = $"Current title: {game.Title}\nCurrent engine: {(game.Engine.Length == 0 ? "Not set" : game.Engine)}";
        Installations.ItemsSource = game.Installations; Installations.SelectedIndex = game.Installations.Count > 0 ? 0 : -1;
        Evidence.Text = string.Join("\n", game.DetectionHistory.TakeLast(5).Select(h => $"{h.At:g}: {h.Engine} {h.Executable}\n{string.Join("; ", h.Evidence)}"));
        Closing += (_, e) => { if (scan is not null) { scan.Cancel(); e.Cancel = true; Status.Text = "Canceling scan; close again when it stops."; } };
    }
    private void InstallationChanged(object sender, SelectionChangedEventArgs e)
    {
        report = null;
        if (ApplyButton is null) return;
        ApplyButton.IsEnabled = false; Candidates.ItemsSource = null; Engines.ItemsSource = null;
        UseCandidate.IsChecked = UseEngine.IsChecked = UseTitle.IsChecked = false;
        TitleSuggestion.Text = Installations.SelectedItem is GameInstallation i ? FilenameParser.FromLocation(i.Path, true).Title + "\nSource: " + DisplayPaths.Compact(i.Path) : "Link or extract an installation first.";
    }
    private async void Scan(object sender, RoutedEventArgs e)
    {
        if (scan is not null || Installations.SelectedItem is not GameInstallation installation) return;
        scan = new(); ScanButton.IsEnabled = Installations.IsEnabled = ApplyButton.IsEnabled = false;
        UseCandidate.IsChecked = UseEngine.IsChecked = UseTitle.IsChecked = false;
        try
        {
            report = await LaunchDetection.ScanAsync(installation, scan.Token);
            Candidates.ItemsSource = report.Candidates; Engines.ItemsSource = report.Engines;
            Evidence.Text = string.Join("\n", report.Notes); ApplyButton.IsEnabled = true;
            Status.Text = $"{report.Candidates.Count} candidates, {report.Engines.Count} engine hints. Select evidence and check only the changes you want.";
        }
        catch (Exception ex) { report = null; Status.Text = ex is OperationCanceledException ? "Scan canceled." : ex.Message; }
        finally { scan.Dispose(); scan = null; ScanButton.IsEnabled = Installations.IsEnabled = true; }
    }
    private void CandidateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (report is null || Evidence is null) return;
        Evidence.Text = (Candidates.SelectedItem is LaunchCandidate c ? DisplayPaths.Compact(c.Path) + "\n" + string.Join("\n", c.Evidence) + "\n\n" : "") + string.Join("\n", report.Notes);
    }
    private void Apply(object sender, RoutedEventArgs e)
    {
        if (report is null) return;
        try
        {
            if (UseCandidate.IsChecked != true && UseEngine.IsChecked != true && UseTitle.IsChecked != true) throw new InvalidOperationException("Select at least one change to apply.");
            if (UseCandidate.IsChecked == true && Candidates.SelectedItem is null) throw new InvalidOperationException("Select an executable.");
            if (UseEngine.IsChecked == true && Engines.SelectedItem is null) throw new InvalidOperationException("Select an engine suggestion.");
            var result = LaunchDetection.Apply(source, game.Id, report, UseCandidate.IsChecked == true ? ((LaunchCandidate)Candidates.SelectedItem).Path : null,
                UseEngine.IsChecked == true ? ((EngineSuggestion)Engines.SelectedItem).Name : null,
                UseTitle.IsChecked == true ? FilenameParser.FromLocation(report.Root, true).Title : null);
            save(result); DialogResult = true;
        }
        catch (Exception ex) { Status.Text = ex.Message; }
    }
    private void CloseWindow(object sender, RoutedEventArgs e) => Close();
}
