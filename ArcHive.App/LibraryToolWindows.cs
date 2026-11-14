using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArcHive.Core;
namespace ArcHive.App;

public class ToolWindow : Window
{
    protected readonly StackPanel Body = new() { Margin = new Thickness(24) };
    protected readonly TextBlock StatusText = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 10) };
    public ToolWindow(string title)
    {
        Title = title; Width = 780; Height = 720; MinWidth = 540; MinHeight = 420; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(8, 13, 11)); Foreground = Brushes.White;
        Content = new ScrollViewer { Content = Body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Label(title, 24);
    }
    protected void Label(string text, double size = 13) => Body.Children.Add(new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) });
    protected Button ActionButton(string title, Action action)
    {
        var b = new Button { Content = title, HorizontalAlignment = HorizontalAlignment.Left };
        b.Click += (_, _) => { try { action(); } catch (Exception ex) { StatusText.Text = ex.Message; } }; Body.Children.Add(b); return b;
    }
    protected TextBox Input(string label, string text)
    { Label(label); var box = new TextBox { Text = text }; System.Windows.Automation.AutomationProperties.SetName(box, label); Body.Children.Add(box); return box; }
    protected ComboBox Options(string label, IEnumerable<string> choices, string selected)
    { Label(label); var c = new ComboBox { ItemsSource = choices.ToArray(), SelectedItem = selected }; System.Windows.Automation.AutomationProperties.SetName(c, label); Body.Children.Add(c); return c; }
}
public sealed class BrowseWindow : ToolWindow
{
    public BrowseOptions Result { get; private set; }
    public BrowseWindow(IEnumerable<Game> source, BrowseOptions current) : base("Browse Library")
    {
        Result = current; var games = source.ToArray(); var stats = LibraryBrowsing.Statistics(games);
        Label($"{stats.Games} games · {stats.Favorites} favorites · {stats.Completed} completed · {stats.Hours:0.0} hours played");
        var collections = Options("Collection", new[] { "" }.Concat(games.SelectMany(g => g.Collections).Distinct(StringComparer.OrdinalIgnoreCase).Order()), current.Collection);
        var tags = Options("Tag", new[] { "" }.Concat(games.SelectMany(g => g.Tags.Split(',')).Select(t => t.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Order()), current.Tag);
        var engines = Options("Game Engine", new[] { "" }.Concat(games.Select(g => g.Engine).Where(t => t.Length > 0).Distinct().Order()), current.Engine);
        var availability = Options("Installation Status", ["", "Ready to Play", "Folder Linked", "Archived", "Missing Files", "Not Installed"], current.Installation);
        var controller = Options("Controller Support", ["All Games", "Controller Supported"], current.ControllerOnly ? "Controller Supported" : "All Games");
        var played = Options("Play History", ["All Games", "Unplayed"], current.UnplayedOnly ? "Unplayed" : "All Games");
        var sort = Options("Sort By", ["Library Settings", "Title", "Recently Played", "Playtime"], current.Sort);
        Label("Filters apply together with search, Play Status, favorites and NSFW visibility. Statistics cover the library allowed by your NSFW setting.");
        ActionButton("Apply Filters", () => { Result = new(collections.SelectedItem as string ?? "", tags.SelectedItem as string ?? "", engines.SelectedItem as string ?? "", controller.SelectedIndex == 1, played.SelectedIndex == 1, availability.SelectedItem as string ?? "", sort.SelectedItem as string ?? "Title"); DialogResult = true; });
        ActionButton("Clear Browse Filters", () => { Result = new(); DialogResult = true; }); ActionButton("Cancel", () => DialogResult = false); Body.Children.Add(StatusText);
    }
}
public sealed class GameHistoryWindow : ToolWindow
{
    public GameHistoryWindow(Game game, Action save) : base("Playtime & Naming — " + game.Title)
    {
        Label($"Recorded Playtime: {game.PlaytimeLabel}", 18);
        var sessions = new ListBox { ItemsSource = game.Sessions.OrderByDescending(s => s.Started).ToArray(), MaxHeight = 160 }; Body.Children.Add(sessions);
        var minutes = Input("Correct Selected Session (Minutes)", "0");
        sessions.SelectionChanged += (_, _) => { if (sessions.SelectedItem is PlaySession s) minutes.Text = (s.Seconds / 60).ToString("0.##", CultureInfo.CurrentCulture); };
        ActionButton("Save Duration", () => { if (sessions.SelectedItem is not PlaySession s) throw new InvalidOperationException("Select a session."); var old = JsonSerializer.Serialize(s); try { Playtime.Correct(s, double.Parse(minutes.Text, CultureInfo.CurrentCulture)); save(); } catch { var restored = JsonSerializer.Deserialize<PlaySession>(old)!; s.Seconds = restored.Seconds; s.NeedsReview = restored.NeedsReview; s.Note = restored.Note; throw; } sessions.Items.Refresh(); StatusText.Text = "Duration saved."; });
        ActionButton("Add Manual Session", () => { var s = new PlaySession { Started = DateTimeOffset.UtcNow, Checkpoint = DateTimeOffset.UtcNow }; Playtime.Correct(s, double.Parse(minutes.Text, CultureInfo.CurrentCulture)); game.Sessions.Add(s); try { save(); } catch { game.Sessions.Remove(s); throw; } sessions.ItemsSource = game.Sessions.OrderByDescending(x => x.Started).ToArray(); });
        Label("Keep ArcHive open while playing. Sessions include observed child processes. Fast launcher handoffs and interrupted monitoring may need a manual correction.");
        var proposal = NamingReview.Suggest(game);
        Label("Naming Review", 18); Label($"Original Name: {proposal.OriginalName}\nDisplay Title: {game.Title}\nSuggested Title: {proposal.SuggestedTitle}\nParser Confidence: {proposal.Confidence}\n" + string.Join("\n", proposal.Evidence));
        var title = Input("Canonical Title", game.Naming.CanonicalTitle.Length > 0 ? game.Naming.CanonicalTitle : proposal.SuggestedTitle);
        var display = Options("Display Title", ["Keep Current Display Title", "Use Canonical Title"], "Keep Current Display Title");
        ActionButton("Apply Naming", () => { var original = game.Naming; var oldTitle = game.Title; var metadata = JsonSerializer.Serialize(game.Metadata); try { NamingReview.Apply(game, proposal, title.Text, display.SelectedIndex == 1); save(); } catch { game.Naming = original; game.Title = oldTitle; game.Metadata = JsonSerializer.Deserialize<MetadataState>(metadata)!; throw; } StatusText.Text = "Naming saved. Original files were not renamed."; });
        if (game.MergedRecords.Count > 0)
        {
            Label("Preserved Merge Records", 18);
            var records = new TextBox { Text = JsonSerializer.Serialize(game.MergedRecords, new JsonSerializerOptions { WriteIndented = true }), IsReadOnly = true, TextWrapping = TextWrapping.NoWrap, Height = 180, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto }; Body.Children.Add(records);
        }
        Body.Children.Add(StatusText); ActionButton("Close", Close);
    }
}
