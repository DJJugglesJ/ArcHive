using System.Windows;
namespace ArcHive.App;
public partial class MainWindow
{
    private void PurgeLibrary() => Guard(() =>
    {
        if (operation is not null || extractionQueue?.IsRunning == true) throw new InvalidOperationException("Wait for current operations to finish before purging the library.");
        if (System.IO.File.Exists(System.IO.Path.Combine(data, "file-operations.json"))) throw new InvalidOperationException("Recover pending file changes before purging the library.");
        if (monitored.Count > 0) throw new InvalidOperationException("Finish active play sessions before purging the library.");
        if (!ChoiceDialog.Confirm(this, "Purge Library", $"Remove all {library.Games.Count} games, saved ArcHive information, library folder registrations and extraction queue history?\n\nOriginal game folders, archives, save files, artwork files and backup snapshots will remain on disk. Settings, credentials and plugins will remain configured.\n\nA recovery copy of the library database will be kept. This starts an empty library; it does not delete game files.", "Purge Library")) return;
        Commit(); store.Save(library);
        var backup = store.Purge();
        selected = null; Games.SelectedItem = null; library = store.Load();
        store.CompletePendingPurge(extractionQueue!.ClearForLibraryReset);
        if (detailsPage) BackToCollection(this, new());
        Navigate(false, false); Refresh();
        Status.Text = "Library purged. Game files were retained. Recovery database: " + backup;
    });
}
