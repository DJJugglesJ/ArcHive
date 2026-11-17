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
        var columns = Math.Max(1, (int)(e.NewSize.Width / (preferences.LibraryDensity == LibraryDensity.Compact ? 190 : 240)));
        if (columns == galleryColumns) return;
        galleryColumns = columns; RebuildGallery();
    }
    private void SelectCard(object sender, RoutedEventArgs e)
    { Games.SelectedItem = ((Button)sender).Tag; ShowDetailsPage(); }
    private void SelectArchiveRow(object sender, SelectionChangedEventArgs e)
    { if (ArchiveList.SelectedItem is Game game) Games.SelectedItem = game; }
    private void NavigateLibrary(object sender, RoutedEventArgs e) => Navigate(false, false);
    private void NavigateFavorites(object sender, RoutedEventArgs e) => Navigate(false, true);
    private void NavigateArchives(object sender, RoutedEventArgs e) => Navigate(true, false);
    private void Navigate(bool archives, bool favorites)
    {
        if (operation is not null) return;
        if (detailsPage) BackToCollection(this, new());
        Commit(); archivesView = archives;
        FavoritesOnly.IsChecked = favorites;
        PageTitle.Text = archives ? "Archives" : favorites ? "Favorites" : "Library";
        PageSubtitle.Text = archives ? "Inspect your downloads. Extract when you are ready." : "Your games, all in one place.";
        Gallery.Visibility = archives ? Visibility.Collapsed : Visibility.Visible;
        ArchiveList.Visibility = archives ? Visibility.Visible : Visibility.Collapsed;
        Refresh(); (archives ? (Control)ArchiveList : Gallery).Focus();
    }
    private void ToggleInspector(object sender, RoutedEventArgs e)
    { if (selected is null) Status.Text = "Select a game to view its details."; else ShowDetailsPage(); }

    public MainWindow()
    {
        InitializeComponent();
        LoadPreferences();
        ProfileArgumentRows.ItemsSource = argumentRows;
        ControllerSupportBox.ItemsSource = Enum.GetValues<ControllerSupport>();
        StartGamepad();
#if MULTI_ARCHIVE
        ExtractButton.Content = "Manage Archive…";
#endif
        store = new SqliteLibraryStore(Path.Combine(data, "library.db"), Path.Combine(data, "library.json"));
        try { library = store.Load(); extractionQueue = new ExtractionQueue(Path.Combine(data, "extraction-queue.json")); store.CompletePendingPurge(extractionQueue.ClearForLibraryReset); LibraryFileMoves.Recover(library, Path.Combine(data, "file-operations.json")); }
        catch (Exception ex)
        {
            ErrorDialog.ShowDetails(this, "Your library or an operation journal could not be loaded safely. Existing files have been retained. Check the recovery guides; another ArcHive instance may own this data folder.", ex);
            Application.Current.Shutdown(); return;
        }
        ProgressBox.ItemsSource = Enum.GetValues<GameProgress>();
        StatusFilter.ItemsSource = new[] { "All Play Statuses" }.Concat(Enum.GetNames<GameProgress>()).ToArray();
        StatusFilter.SelectedIndex = 0;
        ready = true;
        StartPlaytime();
        Refresh();
        Loaded += (_, _) => { Gallery.Focus(); CheckStartupUpdate(); };
        Closing += OnClosing;
        Closed += (_, _) => extractionQueue?.Dispose();
        if (extractionQueue.Jobs.Any(j => j.State != ExtractionState.Completed && j.State != ExtractionState.Cancelled))
            Status.Text = "Extraction jobs await review. Open Extraction Queue to run pending jobs or recover interrupted work.";
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
        selected.IsNsfw = NsfwBox.IsChecked == true;
        selected.Favorite = FavoriteBox.IsChecked == true;
        selected.Played = PlayedBox.IsChecked == true;
        CommitMilestone();
    }
    private void Refresh()
    {
        var current = selected;
        var profileId = editorProfile?.Id;
        Games.ItemsSource = library.Games.Where(g =>
            preferences.Includes(g) && (activeTag is null || GameTags.Contains(g.Tags, activeTag)) && (!archivesView || g.Archives.Count > 0) &&
            (StatusFilter.SelectedIndex <= 0 || g.Progress.ToString() == StatusFilter.SelectedItem as string) &&
            (FavoritesOnly.IsChecked != true || g.Favorite) &&
            (g.Title.Contains(Search.Text, StringComparison.OrdinalIgnoreCase) || g.Tags.Contains(Search.Text, StringComparison.OrdinalIgnoreCase) || g.Naming.CanonicalTitle.Contains(Search.Text, StringComparison.OrdinalIgnoreCase) || g.Naming.OriginalName.Contains(Search.Text, StringComparison.OrdinalIgnoreCase) || g.Collections.Any(c => c.Contains(Search.Text, StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(g => preferences.LibraryOrder == LibraryOrder.FavoritesFirst && g.Favorite)
            .ThenByDescending(g => preferences.LibraryOrder == LibraryOrder.RecentlyPlayed ? g.LastLaunched : null)
            .ThenBy(g => g.Title).ToList();
        Games.ItemsSource = LibraryBrowsing.Apply(((IEnumerable<Game>)Games.ItemsSource), browse).ToList();
        Games.SelectedItem = current is not null && Games.Items.Contains(current) ? current : null;
        if (editorInstallation?.Profiles.FirstOrDefault(p => p.Id == profileId) is { } profile) ProfileChoices.SelectedItem = profile;
        ArchiveList.ItemsSource = Games.ItemsSource;
        ArchiveList.SelectedItem = Games.SelectedItem;
        RebuildGallery();
        ShowTagFilters();
        EmptyTitle.Text = library.Games.Count == 0 ? UiText.EmptyTitle : UiText.NoMatches;
        EmptyDescription.Text = library.Games.Count == 0 ? UiText.EmptyDescription : UiText.NoMatchesDescription;
        EmptyState.Visibility = Games.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Status.Text = UiText.LibraryCount(Games.Items.Count, library.Games.Count, library.Roots.Count);
    }
    private void FilterChanged(object sender, RoutedEventArgs e)
    { if (ready) { Commit(); Refresh(); } }
    private void SelectGame(object sender, SelectionChangedEventArgs e)
    {
        Commit();
        selected = Games.SelectedItem as Game;
        foreach (var game in library.Games) game.IsSelected = game == selected;
        editorArchive = null; editorInstallation = null; editorProfile = null;
        ArchiveList.SelectedItem = selected;
        SelectedSource.Text = selected?.CompactSourceSummary ?? "Select a game to see its source locations.";
        SelectedSource.ToolTip = selected?.SourceSummary;
        Details.IsEnabled = selected is not null;
        if (selected is null)
        {
            TitleBox.Clear(); TagsBox.Clear(); DescriptionBox.Clear(); ReleaseYearBox.Clear(); EngineBox.Clear();
            FavoriteBox.IsChecked = PlayedBox.IsChecked = NsfwBox.IsChecked = false;
            ArchiveChoices.ItemsSource = null; FolderChoices.ItemsSource = null;
            Screenshot.ImagePath = null; Screenshot.Reference = null; ScreenshotStrip.ItemsSource = null; ArtworkSources.Text = ""; ControllerSupportBox.SelectedItem = null; DetailTitle.Text = "Select a game"; DetailState.Text = ""; Paths.Text = "Select a game to view its details.";
            ClearMilestone();
            return;
        }
        DetailTitle.Text = selected.Title; OverviewDescription.Text = string.IsNullOrWhiteSpace(selected.Description) ? UiText.NoDescription : selected.Description; DetailState.Text = $"Installation Status: {selected.InstallationStatus} · Play Status: {selected.Progress} · Update Status: {selected.UpdateStatus}";
        TitleBox.Text = selected.Title;
        ReleaseYearBox.Text = selected.ReleaseYear; EngineBox.Text = selected.Engine; ControllerSupportBox.SelectedItem = selected.ControllerSupport;
        TagsBox.Text = selected.Tags;
        ShowDetailTags();
        DescriptionBox.Text = selected.Description;
        FavoriteBox.IsChecked = selected.Favorite; NsfwBox.IsChecked = selected.IsNsfw;
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
        OpenGameFolderButton.IsEnabled = ActiveInstallation is not null && Directory.Exists(ActiveInstallation.Path);
        if (Paths is null) return;
        PrimaryActionLabel.Text = GameWorkflow.Label(GameWorkflow.Primary(ActiveInstallation, editorProfile, ActiveArchive));
        System.Windows.Automation.AutomationProperties.SetName(PlayButton, PrimaryActionLabel.Text);
        ExtractButton.Visibility = ActiveArchive is null ? Visibility.Collapsed : Visibility.Visible;
        var parsed = FilenameParser.Parse(Path.GetFileName(ActiveArchive?.Path ?? ActiveInstallation?.Path ?? ""));
        Paths.Text = $"Filename hints: version {parsed.Version ?? "unknown"} / {parsed.Platform ?? "unknown"}\nArchive: {(ActiveArchive is null ? "—" : DisplayPaths.Compact(ActiveArchive.Path))}\nFolder: {(ActiveInstallation is null ? "—" : DisplayPaths.Compact(ActiveInstallation.Path))}\nLaunch: {(editorProfile is null ? "Choose an executable" : DisplayPaths.Compact(editorProfile.ExecutablePath))}";
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
        var returnFocus = System.Windows.Input.Keyboard.FocusedElement as FrameworkElement;
        operation = new(); Toolbar.IsEnabled = Workspace.IsEnabled = Search.IsEnabled = FavoritesOnly.IsEnabled = StatusFilter.IsEnabled = false;
        CancelButton.IsEnabled = true;
        try { await action(operation.Token); store.Save(library); Refresh(); }
        catch (OperationCanceledException) { Status.Text = "Operation canceled."; }
        catch (Exception ex) { Status.Text = ex.Message; MessageBox.Show(this, ex.Message, "Operation failed"); }
        finally
        {
            operation.Dispose(); operation = null;
            Toolbar.IsEnabled = Workspace.IsEnabled = Search.IsEnabled = FavoritesOnly.IsEnabled = StatusFilter.IsEnabled = true;
            CancelButton.IsEnabled = false;
            if (IsActive && returnFocus is { IsVisible: true, IsEnabled: true }) { returnFocus.Focus(); returnFocus.BringIntoView(); }
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
    private async void Launch(object sender, RoutedEventArgs e) => await Busy(async token =>
    {
        Commit();
        if (selected is not null && !preferences.Includes(selected)) { Refresh(); return; }
        if (ActiveInstallation is not GameInstallation installation || editorProfile is null)
            throw new InvalidOperationException("Choose an installation and set up a launch profile before playing.");
        if (selected!.Sessions.Any(s => s.Active)) throw new InvalidOperationException("This game is already being tracked. Close it before launching again.");
        var adapters = IntegrationSession.Adapters(preferences).ToArray();
        var plan = await Task.Run(() => LaunchService.PrepareAsync(installation, editorProfile, adapters, token), token);
        token.ThrowIfCancellationRequested();
        using var process = Process.Start(plan.ToStartInfo());
        if (process is null) throw new IOException("Windows did not start the game.");
        Track(selected!, process);
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
        if (operation is not null) { operation.Cancel(); e.Cancel = true; Status.Text = "Canceling; close again after the operation stops."; return; }
        try { StopPlaytime(); Commit(); store.Save(library); }
        catch (Exception ex) { playtimeTimer.Start(); e.Cancel = true; MessageBox.Show(this, "Could not save library: " + ex.Message); }
    }
}

