using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ArcHive.Core;
namespace ArcHive.App;
public partial class MainWindow
{
    private void RemoveSelectedEntry() => Guard(() =>
    {
        if (selected is null) return;
        if (operation is not null || extractionQueue?.IsRunning == true || monitored.Count > 0)
            throw new InvalidOperationException("Finish running games and library operations before removing an entry.");
        if (System.IO.File.Exists(System.IO.Path.Combine(data, "file-operations.json")))
            throw new InvalidOperationException("Recover pending file changes before removing an entry.");
        var game = selected;
        if (!ChoiceDialog.Confirm(this, "Remove from Library", $"Remove “{game.Title}” from ArcHive?\n\nIts ArcHive information and play history will be removed. Game folders, archives, saves and backup files stay on disk. Rescanning a registered folder may add the game again.", "Remove from Library")) return;
        Commit();
        var plan = BatchOperations.Preview(library, [game.Id], new(BatchAction.RemoveEntries));
        store.Save(plan.Result);
        Games.SelectedItem=null; selected=null; library=plan.Result;
        if (detailsPage) BackToCollection(this, new()); else Refresh();
        FocusSelectedEntry(); Status.Text="Removed from library. Game files and saves were kept.";
    });
    private void FocusSelectedEntry()
    {
        if (Games.Items.Count == 0) { Search.Focus(); return; }
        if (Games.SelectedIndex < 0) Games.SelectedIndex=0;
        if (archivesView)
        {
            ArchiveList.SelectedItem=Games.SelectedItem; ArchiveList.ScrollIntoView(Games.SelectedItem);
            ArchiveList.UpdateLayout();
            if (ArchiveList.ItemContainerGenerator.ContainerFromItem(Games.SelectedItem) is ListViewItem row) row.Focus();
            else ArchiveList.Focus();
            return;
        }
        var index=Games.SelectedIndex/galleryColumns;
        if (index >= Gallery.Items.Count) return;
        Gallery.ScrollIntoView(Gallery.Items[index]);
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (detailsPage || !IsActive) return;
            Gallery.UpdateLayout();
            Button? Find(DependencyObject parent)
            {
                if (parent is Button button && ReferenceEquals(button.Tag, Games.SelectedItem)) return button;
                for (int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
                    if (Find(VisualTreeHelper.GetChild(parent,i)) is { } match) return match;
                return null;
            }
            (Find(Gallery) as Control ?? Gallery).Focus();
        }));
    }
}
