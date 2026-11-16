using System.Text.Json;
using System.Windows;
using ArcHive.Core;

namespace ArcHive.App;

public partial class MainWindow
{
    private void OpenMetadataSource(object sender, System.Windows.Navigation.RequestNavigateEventArgs e) => MetadataSourceLinks.Open(e);
    private void ClearCover(object sender, RoutedEventArgs e) => EditArtwork(a => a.Cover = null);
    private void ClearBackdrop(object sender, RoutedEventArgs e) => EditArtwork(a => a.Backdrop = null);
    private void RemoveScreenshot(object sender, RoutedEventArgs e) => EditArtwork(a => a.Screenshots.Remove((ArtworkReference)((System.Windows.Controls.Button)sender).Tag));
    private void EditArtwork(Action<GameArtwork> action) => Guard(() =>
    {
        Commit(); if (selected is null) return;
        var copy = JsonSerializer.Deserialize<Game>(JsonSerializer.Serialize(selected))!;
        action(copy.Artwork); SaveMetadata(copy);
    });
    private void OpenF95Thread(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (!preferences.EnableF95Zone) { Status.Text = "Enable F95Zone tools in Settings → Optional sources."; return; }
        Commit(); if (selected is null) return;
        var copy = JsonSerializer.Deserialize<Game>(JsonSerializer.Serialize(selected))!;
        if (new F95ThreadWindow(copy) { Owner = this }.ShowDialog() == true) SaveMetadata(copy);
    });
    private void FindMetadata(object sender, RoutedEventArgs e) => Guard(() =>
    {
        Commit(); if (selected is null) return;
        var copy = JsonSerializer.Deserialize<Game>(JsonSerializer.Serialize(selected))!;
        if (new MetadataWindow(copy, enableF95Zone: preferences.EnableF95Zone) { Owner = this }.ShowDialog() == true) SaveMetadata(copy);
    });
    private void UndoMetadata(object sender, RoutedEventArgs e) => Guard(() =>
    {
        Commit(); if (selected is null) return;
        var copy = JsonSerializer.Deserialize<Game>(JsonSerializer.Serialize(selected))!;
        MetadataReconciliation.Undo(copy); SaveMetadata(copy);
    });
    private void SaveMetadata(Game copy)
    {
        // Monitoring continues while a metadata dialog is open. Keep the latest sessions.
        if (selected is not null && copy.Id == selected.Id) copy.Sessions = selected.Sessions;
        // Save a detached library first; failed persistence cannot change the visible game.
        var updated = new Library { Version = library.Version, Roots = library.Roots, Games = library.Games.Select(g => g == selected ? copy : g).ToList() };
        store.Save(updated);
        selected = null; library = updated;
        Refresh(); Games.SelectedItem = copy;
        if (detailsPage) { DetailsOptionsButton.Focus(); DetailsOptionsButton.BringIntoView(); }
    }
}
