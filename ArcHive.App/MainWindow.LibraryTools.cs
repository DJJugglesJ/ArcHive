using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using ArcHive.Core;
namespace ArcHive.App;
public partial class MainWindow
{
    private readonly Dictionary<Guid, (Guid Session, ProcessSessionMonitor Monitor, Stopwatch Clock)> monitored = [];
    private readonly DispatcherTimer playtimeTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private int playtimeTicks;
    private void StartPlaytime()
    {
        if (Playtime.Recover(library.Games)) store.Save(library);
        playtimeTimer.Tick += (_, _) => CheckPlaytime(); playtimeTimer.Start();
    }
    private void Track(Game game, Process process)
    {
        var session = Playtime.Begin(game, DateTimeOffset.UtcNow);
        try { monitored[game.Id] = (session.Id, new ProcessSessionMonitor(process), Stopwatch.StartNew()); }
        catch (Exception ex) { Playtime.Finish(session, true, "Process ended before monitoring began: " + ex.Message); }
    }
    private void CheckPlaytime()
    {
        if (operation is not null || monitored.Count == 0) return;
        try
        {
            var finished = false;
            foreach (var item in monitored.ToArray())
            {
                var game = library.Games.Single(g => g.Id == item.Key); var session = game.Sessions.Single(s => s.Id == item.Value.Session);
                var elapsed = item.Value.Clock.Elapsed.TotalSeconds; item.Value.Clock.Restart();
                bool alive;
                try { alive = item.Value.Monitor.IsRunning(); }
                catch { Playtime.Finish(session, true, "Process monitoring failed. Review the recorded duration."); monitored.Remove(item.Key); finished = true; continue; }
                Playtime.Tick(session, DateTimeOffset.UtcNow, elapsed);
                if (!alive) { Playtime.Finish(session, session.Seconds < 10 && !item.Value.Monitor.HadChildren, "Observed process session ended. Unobserved launcher handoffs are not tracked."); monitored.Remove(item.Key); finished = true; }
            }
            if (++playtimeTicks % 5 == 0 || finished) { Commit(); store.Save(library); }
        }
        catch (Exception ex) { Status.Text = "Playtime checkpoint failed: " + ex.Message; }
    }
    private void StopPlaytime()
    {
        playtimeTimer.Stop(); CheckPlaytime();
        foreach (var game in library.Games) foreach (var session in game.Sessions.Where(s => s.Active))
            Playtime.Finish(session, true, "ArcHive closed while the game was running. Review this session after playing.");
        monitored.Clear();
    }
    private void OpenGameHistory(object sender, RoutedEventArgs e) => Guard(() =>
    {
        Commit(); if (selected is null) return;
        var game = selected; new GameHistoryWindow(game, () => store.Save(library)) { Owner = this }.ShowDialog(); selected = null; Games.SelectedItem = null; Refresh(); Games.SelectedItem = game;
    });
    private BrowseOptions browse = new();
    private void OpenLibraryFilters(object sender, RoutedEventArgs e) => Guard(() =>
    {
        Commit(); var dialog = new BrowseWindow(library.Games.Where(preferences.Includes), browse) { Owner = this };
        if (dialog.ShowDialog() == true) { browse = dialog.Result; Refresh(); }
    });
}
