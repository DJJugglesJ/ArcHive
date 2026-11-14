using System.Windows;
namespace ArcHive.App;
public partial class MainWindow
{
    private void OpenOwnedLibrary(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (operation is not null) return;
        Commit();
        if (new OwnedLibraryWindow(library, updated => { store.Save(updated); Games.SelectedItem = null; selected = null; library = updated; }) { Owner = this }.ShowDialog() == true)
        { if (detailsPage) BackToCollection(this, new()); else Refresh(); }
    });
}
