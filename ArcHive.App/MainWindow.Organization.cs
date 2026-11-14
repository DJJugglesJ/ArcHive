using System.Windows;
using ArcHive.Core;

namespace ArcHive.App;

public partial class MainWindow
{
    private void OrganizeLibrary(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (operation is not null) return;
        if (monitored.Count > 0) throw new InvalidOperationException("Finish active play sessions before batch organization.");
        Commit(); store.Save(library); Refresh();
        void SaveUpdated(Library updated)
        {
            // Publish in-memory state only after SQLite commits successfully.
            store.Save(updated);
            Games.SelectedItem = null;
            selected = null;
            library = updated;
        }
        var dialog = new OrganizationWindow(library, Games.Items.Cast<Game>(), SaveUpdated, games =>
        {
            new BatchToolsWindow(library, games, SaveUpdated, extractionQueue!, System.IO.Path.Combine(data, "file-operations.json"), store.Load) { Owner = this }.ShowDialog();
        }) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            if (detailsPage && selected is null) BackToCollection(this, new());
            else Refresh();
        }
    });
}
