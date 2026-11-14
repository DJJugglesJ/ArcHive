using System.Text.Json;
using System.Windows;
using ArcHive.Core;

namespace ArcHive.App;

public partial class MainWindow
{
    private void OpenMetadataSource(object sender, System.Windows.Navigation.RequestNavigateEventArgs e) => MetadataSourceLinks.Open(e);
    private void FindMetadata(object sender, RoutedEventArgs e) => Guard(() =>
    {
        Commit(); if (selected is null) return;
        var copy = JsonSerializer.Deserialize<Game>(JsonSerializer.Serialize(selected))!;
        if (new MetadataWindow(copy) { Owner = this }.ShowDialog() == true) SaveMetadata(copy);
    });
    private void UndoMetadata(object sender, RoutedEventArgs e) => Guard(() =>
    {
        Commit(); if (selected is null) return;
        var copy = JsonSerializer.Deserialize<Game>(JsonSerializer.Serialize(selected))!;
        MetadataReconciliation.Undo(copy); SaveMetadata(copy);
    });
    private void SaveMetadata(Game copy)
    {
        // Save a detached library first; failed persistence cannot change the visible game.
        var updated = new Library { Version = library.Version, Roots = library.Roots, Games = library.Games.Select(g => g == selected ? copy : g).ToList() };
        store.Save(updated);
        selected = null; library = updated;
        Refresh(); Games.SelectedItem = copy;
    }
}
