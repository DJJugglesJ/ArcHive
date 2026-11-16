using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ArcHive.Core;
namespace ArcHive.App;
public partial class MainWindow
{
    private bool detailsPage;
    private int detailAction;
    private Button[] DetailButtons => [PlayButton, DetailsOptionsButton, BackToLibrary];
    private void ShowDetailsPage()
    {
        if (selected is null || operation is not null) return;
        detailsPage = true; detailAction = 0; GameSections.SelectedIndex = 0; CollectionActions.Visibility = Visibility.Collapsed;
        BrowsePane.Visibility = Visibility.Collapsed; InspectorBorder.Visibility = Visibility.Visible;
        Grid.SetColumn(InspectorBorder, 0); Grid.SetColumnSpan(InspectorBorder, 2); InspectorColumn.Width = new GridLength(0);
        BackToLibrary.Visibility = Visibility.Visible; InspectorToggle.Visibility = Visibility.Collapsed;
        SearchFilters.Visibility = Visibility.Collapsed;
        PageTitle.Text = "Game Details"; PageSubtitle.Text = DisplayPaths.Compact(selected.SourceLocation);
        DetailsScroller.ScrollToTop(); PlayButton.Focus();
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() => { if (detailsPage && IsActive) PlayButton.Focus(); }));
        if (preferences.GamepadEnabled) { var prompts = ControllerPrompts.For(preferences.ControllerProfile); ControllerNavigationHint.Text = UiText.ControllerHint(preferences.ControllerProfile); }
    }
    private void BackToCollection(object sender, RoutedEventArgs e)
    {
        if (operation is not null) return;
        Commit(); detailsPage = false; ShowPreferences();
        InspectorBorder.Visibility = Visibility.Collapsed; BrowsePane.Visibility = Visibility.Visible;
        BackToLibrary.Visibility = Visibility.Collapsed; InspectorToggle.Visibility = Visibility.Collapsed; CollectionActions.Visibility = Visibility.Visible;
        SearchFilters.Visibility = Visibility.Visible;
        PageTitle.Text = archivesView ? "Archives" : FavoritesOnly.IsChecked == true ? "Favorites" : "Library";
        PageSubtitle.Text = archivesView ? "Inspect your downloads. Extract when you are ready." : "Your games, all in one place.";
        Refresh(); (archivesView ? (Control)ArchiveList : Gallery).Focus();
    }
    private void PrimaryGameAction(object sender, RoutedEventArgs e)
    {
        if (selected is null || operation is not null) return;
        Commit();
        switch (GameWorkflow.Primary(ActiveInstallation, editorProfile, ActiveArchive))
        {
            case GamePrimaryAction.Play: Launch(sender, e); break;
            case GamePrimaryAction.Extract: Extract(sender, e); break;
            case GamePrimaryAction.LinkFolder: LinkFolder(sender, e); break;
            default: GameSections.SelectedIndex = 3; ProfileChoices.Focus(); ProfileChoices.BringIntoView(); Status.Text = "Choose an executable or use Scan Game Files in Launch."; break;
        }
    }
    private void CopySourcePaths(object sender, RoutedEventArgs e) => Guard(() => { if (selected is not null) Clipboard.SetText(selected.SourceSummary); });
    private void OpenArchiveDetails(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || operation is not null) return;
        var row = ItemsControl.ContainerFromElement(ArchiveList, e.OriginalSource as DependencyObject) as ListViewItem;
        if (row?.Content is Game game) { Games.SelectedItem = game; ShowDetailsPage(); e.Handled = true; }
    }
    private void HandleDetailNavigation(PadAction action)
    {
        if (action is PadAction.Next or PadAction.Previous) { GameSections.SelectedIndex = (GameSections.SelectedIndex + (action == PadAction.Next ? 1 : GameSections.Items.Count - 1)) % GameSections.Items.Count; return; }
        if (action == PadAction.Cancel) { BackToCollection(this, new()); return; }
        if (action is PadAction.Up or PadAction.Left or PadAction.Down or PadAction.Right)
        {
            detailAction = (detailAction + (action is PadAction.Up or PadAction.Left ? DetailButtons.Length - 1 : 1)) % DetailButtons.Length;
            DetailButtons[detailAction].Focus(); return;
        }
        if (action == PadAction.Accept)
        {
            if (Keyboard.FocusedElement is Button focused && DetailButtons.Contains(focused)) focused.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            else DetailButtons[detailAction].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
    }
}
