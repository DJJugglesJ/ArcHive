using System.Windows;
using ArcHive.Core;

namespace ArcHive.App;

public partial class MainWindow
{
    private void OrganizeLibrary(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (operation is not null) return;
        Commit(); Refresh();
        var dialog = new OrganizationWindow(library, Games.Items.Cast<Game>(), updated =>
        {
            // Publish in-memory state only after SQLite commits successfully.
            store.Save(updated);
            Games.SelectedItem = null;
            selected = null;
            library = updated;
        }) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            if (detailsPage && selected is null) BackToCollection(this, new());
            else Refresh();
        }
    });
}
