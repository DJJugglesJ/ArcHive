using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ArcHive.Core;
namespace ArcHive.App;
public partial class MainWindow
{
    private ContextMenu? optionsMenu;
    private readonly List<(MenuItem Item, Action Action)> optionActions = [];
    private int optionIndex;
    private void DetectLaunch(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (selected is null || operation is not null) return;
        Commit(); var id = selected.Id; Library? updated = null;
        var dialog = new DetectionWindow(library, selected, result => { store.Save(result); updated = result; }) { Owner = this };
        if (dialog.ShowDialog() == true && updated is not null)
        {
            Games.SelectedItem = null; selected = null; library = updated; Refresh(); Games.SelectedItem = library.Games.FirstOrDefault(g => g.Id == id && preferences.Includes(g));
        }
    });
    private void OpenSource(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (selected is null) return;
        var path = ActiveInstallation?.Path ?? ActiveArchive?.Path ?? selected.SourceLocation;
        if (File.Exists(path)) path = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException("Source folder is unavailable: " + path);
        PathSafety.NoLinks(path);
        var info = new ProcessStartInfo("explorer.exe") { UseShellExecute = false }; info.ArgumentList.Add(Path.GetFullPath(path)); Process.Start(info)?.Dispose();
    });
    private void OpenAddGames(object sender, RoutedEventArgs e)
    {
        if (operation is not null) return;
        var menu = new ContextMenu { PlacementTarget = (FrameworkElement)sender, Placement = PlacementMode.Bottom };
        void Add(string title, RoutedEventHandler action) { var item = new MenuItem { Header = title }; item.Click += action; menu.Items.Add(item); }
        Add("Scan a folder of games or archives", AddRoot);
        Add("Import owned Steam / GOG titles", OpenOwnedLibrary);
        menu.IsOpen = true;
    }
    private void OpenOptions(object sender, RoutedEventArgs e) => ShowOptions();
    private void CardRightClick(object sender, MouseButtonEventArgs e)
    {
        if (operation is not null) return;
        Games.SelectedItem = ((Button)sender).Tag; ShowOptions((FrameworkElement)sender); e.Handled = true;
    }
    private void ArchiveRightClick(object sender, MouseButtonEventArgs e)
    {
        var item = ItemsControl.ContainerFromElement(ArchiveList, e.OriginalSource as DependencyObject) as ListViewItem;
        if (item?.Content is not Game game || operation is not null) return;
        Games.SelectedItem = game; ShowOptions(item); e.Handled = true;
    }
    private void ShowOptions(FrameworkElement? anchor = null)
    {
        if (selected is null || operation is not null || !preferences.Includes(selected)) return;
        if (optionsMenu?.IsOpen == true) { optionsMenu.IsOpen = false; return; }
        Commit(); optionActions.Clear(); optionIndex = 0;
        optionsMenu = new ContextMenu { PlacementTarget = anchor ?? (detailsPage ? PlayButton : archivesView ? ArchiveList : Gallery), Placement = anchor is null ? PlacementMode.Center : PlacementMode.Bottom };
        void Add(string label, Action action, bool enabled = true)
        {
            var item = new MenuItem { Header = label, IsEnabled = enabled };
            item.Click += (_, _) => { optionsMenu.IsOpen = false; action(); };
            optionsMenu.Items.Add(item); if (enabled) optionActions.Add((item, action));
        }
        Add("Open game details", ShowDetailsPage);
        Add("Launch / engine suggestions", () => DetectLaunch(this, new()), selected.Installations.Count > 0);
        Add("Preview archive / queue", () => Extract(this, new()), selected.Archives.Count > 0);
        Add("Open source folder", () => OpenSource(this, new()));
        Add("Save backups and restore", () => OpenSaveBackups(this, new()));
        if (preferences.EnableF95Zone) Add("F95Zone thread / updates", () => OpenF95Thread(this, new()));
        Add("Find metadata", () => FindMetadata(this, new()));
        Add("Organize current view", () => OrganizeLibrary(this, new()));
        optionsMenu.Opened += (_, _) => Dispatcher.BeginInvoke(() => optionActions.FirstOrDefault().Item?.Focus());
        optionsMenu.IsOpen = true;
    }
    private void HandleNavigation(PadAction action)
    {
        if (action == PadAction.None || operation is not null) return;
        if (optionsMenu?.IsOpen == true)
        {
            if (action is PadAction.Cancel or PadAction.Options) { optionsMenu.IsOpen = false; return; }
            if (action is PadAction.Up or PadAction.Down && optionActions.Count > 0)
            { optionIndex = (optionIndex + (action == PadAction.Up ? optionActions.Count - 1 : 1)) % optionActions.Count; optionActions[optionIndex].Item.Focus(); }
            if (action == PadAction.Accept && optionActions.Count > 0) { var entry = optionActions[optionIndex]; optionsMenu.IsOpen = false; entry.Action(); }
            return;
        }
        if (action == PadAction.Options) { ShowOptions(); return; }
        if (detailsPage) { HandleDetailNavigation(action); return; }
        if (action == PadAction.Accept && LibraryInteraction.Accept(detailsPage) == LibraryAcceptAction.OpenDetails) { ShowDetailsPage(); return; }
        if (action == PadAction.Cancel) { (archivesView ? (Control)ArchiveList : Gallery).Focus(); return; }
        if (action is PadAction.Next or PadAction.Previous)
        {
            var current = archivesView ? 2 : FavoritesOnly.IsChecked == true ? 1 : 0;
            var next = (current + (action == PadAction.Next ? 1 : 2)) % 3; Navigate(next == 2, next == 1); return;
        }
        var index = GamepadNavigation.Move(Games.SelectedIndex, Games.Items.Count, archivesView ? 1 : galleryColumns, action);
        if (index < 0) return;
        Games.SelectedIndex = index;
        if (archivesView) { ArchiveList.SelectedItem = Games.SelectedItem; ArchiveList.ScrollIntoView(Games.SelectedItem); }
        else if (index / galleryColumns < Gallery.Items.Count) Gallery.ScrollIntoView(Gallery.Items[index / galleryColumns]);
    }
    private void WindowOptionsKey(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (detailsPage && optionsMenu?.IsOpen != true && Keyboard.FocusedElement is not (System.Windows.Controls.Primitives.TextBoxBase { IsReadOnly: false } or ComboBox or ComboBoxItem))
        {
            var action = key switch { Key.Escape => PadAction.Cancel, Key.Left => PadAction.Left, Key.Right => PadAction.Right, _ => PadAction.None };
            if (action != PadAction.None) { HandleDetailNavigation(action); e.Handled = true; return; }
        }
        if ((key == Key.Apps || key == Key.F10 && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) && selected is not null && operation is null)
        { HandleNavigation(PadAction.Options); e.Handled = true; }
    }
    private void LibraryKey(object sender, KeyEventArgs e)
    {
        if (operation is not null || Keyboard.FocusedElement is TextBoxBase { IsReadOnly: false } or PasswordBox or ComboBox or ComboBoxItem || optionsMenu?.IsOpen == true) return;
        var action = e.Key switch { Key.Up => PadAction.Up, Key.Down => PadAction.Down, Key.Left => PadAction.Left, Key.Right => PadAction.Right,
            Key.Enter => PadAction.Accept, Key.Apps => PadAction.Options, Key.F10 when Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) => PadAction.Options, _ => PadAction.None };
        if (action == PadAction.None) return;
        HandleNavigation(action); e.Handled = true;
    }
}
