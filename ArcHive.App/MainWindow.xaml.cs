using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using ArcHive.Core;
using Microsoft.Win32;

namespace ArcHive.App;

public partial class MainWindow : Window
{
    private readonly string data = Environment.GetEnvironmentVariable("ARCHIVE_DATA_DIRECTORY") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArcHive");
    private readonly SqliteLibraryStore store;
    private Library library = new();
    private Game? selected;
    private CancellationTokenSource? operation;
    private bool ready;
    private bool archivesView;
    private int galleryColumns = 3;
    public sealed record GalleryRow(int Columns, Game[] Games);

    private void RebuildGallery()
    {
        if (!ready) return;
        Gallery.ItemsSource = Games.Items.Cast<Game>().Chunk(galleryColumns)
            .Select(g => new GalleryRow(galleryColumns, g)).ToList();
    }
    private void GalleryResized(object sender, SizeChangedEventArgs e)
    {
        var columns = Math.Max(1, (int)(e.NewSize.Width / 190));
        if (columns == galleryColumns) return;
        galleryColumns = columns; RebuildGallery();
    }
    private void SelectCard(object sender, RoutedEventArgs e)
    { Games.SelectedItem = ((Button)sender).Tag; }
    private void SelectArchiveRow(object sender, SelectionChangedEventArgs e)
    { if (ArchiveList.SelectedItem is Game game) Games.SelectedItem = game; }
    private void NavigateLibrary(object sender, RoutedEventArgs e) => Navigate(false, false);
    private void NavigateFavorites(object sender, RoutedEventArgs e) => Navigate(false, true);
    private void NavigateArchives(object sender, RoutedEventArgs e) => Navigate(true, false);
    private void Navigate(bool archives, bool favorites)
    {
        if (operation is not null) return;
        Commit(); archivesView = archives;
        FavoritesOnly.IsChecked = favorites;
        PageTitle.Text = archives ? "Archive manager" : favorites ? "Your favorites" : "Your library";
        PageSubtitle.Text = archives ? "Inspect your downloads. Extract when you are ready." : "A home for every game you discover.";
        Gallery.Visibility = archives ? Visibility.Collapsed : Visibility.Visible;
        ArchiveList.Visibility = archives ? Visibility.Visible : Visibility.Collapsed;
        Refresh();
    }
    private void ToggleInspector(object sender, RoutedEventArgs e)
    {
        var show = InspectorBorder.Visibility != Visibility.Visible;
        InspectorBorder.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        InspectorColumn.Width = new GridLength(show ? 360 : 0);
        InspectorToggle.Content = show ? "Hide details" : "Show details";
    }

    public MainWindow()
    {
        InitializeComponent();
        ProfileArgumentRows.ItemsSource = argumentRows;
        ControllerSupportBox.ItemsSource = Enum.GetValues<ControllerSupport>();
        StartGamepad();
#if MULTI_ARCHIVE
        ExtractButton.Content = "Extract archive";
#endif
        store = new SqliteLibraryStore(Path.Combine(data, "library.db"), Path.Combine(data, "library.json"));
        try { library = store.Load(); }
        catch (Exception ex)
        {
            MessageBox.Show("Library could not be loaded. Existing files have been retained. See the migration and recovery guide.\n\n" + ex.Message);
            Application.Current.Shutdown(); return;
        }
        ProgressBox.ItemsSource = Enum.GetValues<GameProgress>();
        StatusFilter.ItemsSource = new[] { "All statuses" }.Concat(Enum.GetNames<GameProgress>()).ToArray();
        StatusFilter.SelectedIndex = 0;
        ready = true;
        Refresh();
        Closing += OnClosing;
    }

    private void Commit()
    {
        if (selected is null) return;
        MetadataReconciliation.SetManual(selected, MetadataField.Title, TitleBox.Text.Trim());
        MetadataReconciliation.SetManual(selected, MetadataField.Tags, TagsBox.Text.Trim());
        MetadataReconciliation.SetManual(selected, MetadataField.Description, DescriptionBox.Text);
        MetadataReconciliation.SetManual(selected, MetadataField.ReleaseYear, ReleaseYearBox.Text.Trim());
        MetadataReconciliation.SetManual(selected, MetadataField.Engine, EngineBox.Text.Trim());
        if (ControllerSupportBox.SelectedItem is ControllerSupport support) MetadataReconciliation.SetManual(selected, MetadataField.ControllerSupport, support.ToString());
        selected.Favorite = FavoriteBox.IsChecked == true;
        selected.Played = PlayedBox.IsChecked == true;
        CommitMilestone();
    }
    private void Refresh()
    {
        var current = selected;
        var profileId = editorProfile?.Id;
        Games.ItemsSource = library.Games.Where(g =>
            (!archivesView || g.Archives.Count > 0) &&
            (StatusFilter.SelectedIndex <= 0 || g.Progress.ToString() == StatusFilter.SelectedItem as string) &&
            (FavoritesOnly.IsChecked != true || g.Favorite) &&
            (g.Title.Contains(Search.Text, StringComparison.OrdinalIgnoreCase) || g.Tags.Contains(Search.Text, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(g => g.Title).ToList();
        Games.SelectedItem = current;
        if (editorInstallation?.Profiles.FirstOrDefault(p => p.Id == profileId) is { } profile) ProfileChoices.SelectedItem = profile;
        ArchiveList.ItemsSource = Games.ItemsSource;
        ArchiveList.SelectedItem = Games.SelectedItem;
        RebuildGallery();
        EmptyState.Visibility = Games.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Status.Text = $"{Games.Items.Count} shown / {library.Games.Count} entries • {library.Roots.Count} library folders (manual rescan)";
    }
    private void FilterChanged(object sender, RoutedEventArgs e)
    { if (ready) { Commit(); Refresh(); } }
    private void SelectGame(object sender, SelectionChangedEventArgs e)
    {
        Commit();
        selected = Games.SelectedItem as Game;
        foreach (var game in library.Games) game.IsSelected = game == selected;
        editorArchive = null; editorInstallation = null; editorProfile = null;
        Details.IsEnabled = selected is not null;
        if (selected is null)
        {
            TitleBox.Clear(); TagsBox.Clear(); DescriptionBox.Clear(); ReleaseYearBox.Clear(); EngineBox.Clear();
            FavoriteBox.IsChecked = PlayedBox.IsChecked = false;
            ArchiveChoices.ItemsSource = null; FolderChoices.ItemsSource = null;
            Screenshot.ImagePath = null; Screenshot.Reference = null; ScreenshotStrip.ItemsSource = null; ArtworkSources.Text = ""; ControllerSupportBox.SelectedItem = null; DetailTitle.Text = "Select a game"; DetailState.Text = ""; Paths.Text = "Select a game to see its details.";
            ClearMilestone();
            return;
        }
        DetailTitle.Text = selected.Title; DetailState.Text = $"{selected.State} · {selected.Progress} · {selected.UpdateState}";
        TitleBox.Text = selected.Title;
        ReleaseYearBox.Text = selected.ReleaseYear; EngineBox.Text = selected.Engine; ControllerSupportBox.SelectedItem = selected.ControllerSupport;
        TagsBox.Text = selected.Tags;
        DescriptionBox.Text = selected.Description;
        FavoriteBox.IsChecked = selected.Favorite;
        PlayedBox.IsChecked = selected.Played;
        LoadGameMilestone();
        ShowPaths();
        ShowImage();
    }
    private GameArchive? ActiveArchive => ArchiveChoices.SelectedItem as GameArchive;
    private GameInstallation? ActiveInstallation => FolderChoices.SelectedItem as GameInstallation;
    private void ShowPaths()
    {
        CommitLocation();
        loadingEditors = true;
        var archiveId = ActiveArchive?.Id;
        var installationId = ActiveInstallation?.Id;
        ArchiveChoices.ItemsSource = selected?.Archives.ToList();
        FolderChoices.ItemsSource = selected?.Installations.ToList();
        ArchiveChoices.SelectedItem = selected?.Archives.FirstOrDefault(a => a.Id == archiveId) ?? selected?.Archives.FirstOrDefault();
        FolderChoices.SelectedItem = selected?.Installations.FirstOrDefault(i => i.Id == installationId) ?? selected?.Installations.FirstOrDefault();
        loadingEditors = false;
        LoadLocationEditors();
        UpdateLocationSummary();
    }
    private void SelectedLocationChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loadingEditors) return;
        CommitLocation(); LoadLocationEditors(); UpdateLocationSummary();
    }
    private void UpdateLocationSummary()
    {
        if (Paths is null) return;
        var parsed = FilenameParser.Parse(Path.GetFileName(ActiveArchive?.Path ?? ActiveInstallation?.Path ?? ""));
        Paths.Text = $"Filename hints: version {parsed.Version ?? "unknown"} / {parsed.Platform ?? "unknown"}\nArchive: {ActiveArchive?.Path ?? "—"}\nFolder: {ActiveInstallation?.Path ?? "—"}\nLaunch: {editorProfile?.ExecutablePath ?? "Choose an executable"}";
    }
    private void AddArchive(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (selected is null) return;
        var dialog = new OpenFileDialog { Filter = "Game archives|*.zip;*.rar;*.7z", Multiselect = true };
        if (dialog.ShowDialog(this) != true) return;
        Commit();
        foreach (var path in dialog.FileNames)
        {
            PathSafety.NoLinks(path);
            if (!selected.Archives.Any(a => Game.SamePath(a.Path, path))) selected.Archives.Add(new() { Path = path, Release = ReleaseInfo.FromPath(path) });
        }
        store.Save(library); ShowPaths();
    });
    private void ShowImage()
    {
        Screenshot.ImagePath = selected?.ScreenshotPath;
        Screenshot.Reference = selected?.Artwork.Backdrop ?? selected?.Artwork.Cover;
        ScreenshotStrip.ItemsSource = selected?.Artwork.Screenshots.ToList();
        string Source(ArtworkReference? a) => a is null ? "Local artwork / placeholder" : $"{a.Provider}/{a.ProviderId} · {a.RetrievedAt:g}";
        ArtworkSources.Text = "Cover: " + Source(selected?.Artwork.Cover) + "\nBackdrop: " + Source(selected?.Artwork.Backdrop);
    }
    private void Guard(Action action)
    { try { action(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "ArcHive"); } }
    private async Task Busy(Func<CancellationToken, Task> action)
    {
        if (operation is not null) return;
        Commit();
        operation = new(); Toolbar.IsEnabled = Workspace.IsEnabled = Search.IsEnabled = FavoritesOnly.IsEnabled = StatusFilter.IsEnabled = false;
        CancelButton.IsEnabled = true;
        try { await action(operation.Token); store.Save(library); Refresh(); }
        catch (OperationCanceledException) { Status.Text = "Operation cancelled."; }
        catch (Exception ex) { Status.Text = ex.Message; MessageBox.Show(this, ex.Message, "Operation failed"); }
        finally
        {
            operation.Dispose(); operation = null;
            Toolbar.IsEnabled = Workspace.IsEnabled = Search.IsEnabled = FavoritesOnly.IsEnabled = StatusFilter.IsEnabled = true;
            CancelButton.IsEnabled = false;
        }
    }
    private async void AddRoot(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Choose your games folder" };
        if (dialog.ShowDialog(this) != true) return;
        await Busy(async token =>
        {
            Status.Text = "Scanning " + dialog.FolderName;
            var result = await Task.Run(() => Scanner.Scan(dialog.FolderName, token), token);
            Scanner.Merge(library, result.Games);
            if (!library.Roots.Contains(dialog.FolderName, StringComparer.OrdinalIgnoreCase)) library.Roots.Add(dialog.FolderName);
            ReportWarnings(result.Warnings);
        });
    }
    private async void Rescan(object sender, RoutedEventArgs e)
    {
        await Busy(async token =>
        {
            var found = new List<Game>(); var warnings = new List<string>();
            foreach (var root in library.Roots)
            {
                Status.Text = "Scanning " + root;
                if (!Directory.Exists(root)) { warnings.Add("Unavailable root: " + root); continue; }
                var result = await Task.Run(() => Scanner.Scan(root, token), token);
                found.AddRange(result.Games); warnings.AddRange(result.Warnings);
            }
            Scanner.Merge(library, found); ReportWarnings(warnings);
        });
    }
    private void ReportWarnings(List<string> warnings)
    { if (warnings.Count > 0) MessageBox.Show(this, string.Join("\n", warnings.Take(12)), "Some folders were skipped"); }
    private void SaveDetails(object sender, RoutedEventArgs e) => Guard(() => { Commit(); store.Save(library); Refresh(); });
    private void Cancel(object sender, RoutedEventArgs e) => operation?.Cancel();
    private async void Extract(object sender, RoutedEventArgs e)
    {
        if (selected is null || ActiveArchive is not GameArchive source) return;
        var archive = source.Path;
        var game = selected;
        var dialog = new OpenFolderDialog { Title = "Choose extraction parent folder (a new game folder will be created)" };
        if (dialog.ShowDialog(this) != true) return;
        await Busy(async token =>
        {
            IArchiveExtractor extractor =
#if MULTI_ARCHIVE
                new MultiArchiveExtractor();
#else
                new ZipExtractor();
#endif
            var folder = await extractor.ExtractAsync(archive, dialog.FolderName,
                new Progress<string>(s => Status.Text = s), token);
            var installation = new GameInstallation { Path = folder, SourceArchiveId = source.Id, Release = source.Release with { } };
            game.Installations.Add(installation);
            ShowPaths(); FolderChoices.SelectedItem = installation;
            ShowPaths();
        });
    }
    private void LinkFolder(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (selected is null) return;
        var dialog = new OpenFolderDialog { Title = "Select this game's extracted folder" };
        if (dialog.ShowDialog(this) != true) return;
        PathSafety.NoLinks(dialog.FolderName);
        Commit();
        var installation = selected.Installations.FirstOrDefault(i => Game.SamePath(i.Path, dialog.FolderName));
        if (installation is null)
        {
            installation = new GameInstallation { Path = dialog.FolderName, Release = ReleaseInfo.FromPath(dialog.FolderName) };
            selected.Installations.Add(installation);
        }
        store.Save(library); Refresh(); ShowPaths(); FolderChoices.SelectedItem = installation;
    });
    private void ChooseExecutable(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (ActiveInstallation?.Path is not string folder) throw new InvalidOperationException("Extract or link a folder first.");
        var dialog = new OpenFileDialog { Filter = "Windows executable|*.exe", InitialDirectory = folder, Title = "Choose the game's executable" };
        if (dialog.ShowDialog(this) != true) return;
        if (!PathSafety.IsWithin(folder, dialog.FileName)) throw new IOException("Choose an executable inside the game folder.");
        PathSafety.NoLinks(dialog.FileName);
        Commit();
        if (editorProfile is null) AddProfileModel();
        editorProfile!.ExecutablePath = dialog.FileName;
        editorProfile.WorkingDirectory = Path.GetDirectoryName(dialog.FileName)!;
        LoadLocationEditors(); store.Save(library); UpdateLocationSummary();
    });
    private void Launch(object sender, RoutedEventArgs e) => Guard(() =>
    {
        Commit();
        if (ActiveInstallation is not GameInstallation installation || editorProfile is null)
            throw new InvalidOperationException("Select an installation and configure a launch profile first.");
        var plan = LaunchService.Prepare(installation, editorProfile);
        if (new LaunchConfirmation(plan, GamepadEnabled.IsChecked == true) { Owner = this }.ShowDialog() != true) return;
        using var process = Process.Start(plan.ToStartInfo());
        if (process is null) throw new IOException("Windows did not start the game.");
        Commit(); selected!.Played = true; selected.LastLaunched = DateTimeOffset.UtcNow;
        PlayedBox.IsChecked = true; store.Save(library);
    });
    private void AddScreenshot(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (selected is null) return;
        var dialog = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp" };
        if (dialog.ShowDialog(this) != true) return;
        if (new FileInfo(dialog.FileName).Length > 25 * 1024 * 1024) throw new IOException("Choose an image smaller than 25 MB.");
        var folder = Path.Combine(data, "screenshots"); Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, selected.Id + "-" + Guid.NewGuid().ToString("N") + Path.GetExtension(dialog.FileName));
        File.Copy(dialog.FileName, path); Commit(); selected.ScreenshotPath = path; store.Save(library); ShowImage(); RebuildGallery();
    });
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (operation is not null) { operation.Cancel(); e.Cancel = true; Status.Text = "Cancelling; close again after the operation stops."; return; }
        try { Commit(); store.Save(library); }
        catch (Exception ex) { e.Cancel = true; MessageBox.Show(this, "Could not save library: " + ex.Message); }
    }
}

