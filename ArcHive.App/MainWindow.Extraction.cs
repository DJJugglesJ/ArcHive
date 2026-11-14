using System.Windows;
using ArcHive.Core;
namespace ArcHive.App;
public partial class MainWindow
{
    private ExtractionQueue? extractionQueue;
    private void OpenExtractionQueue(object sender, RoutedEventArgs e) => ShowExtractionQueue(null, null);
    private void Extract(object sender, RoutedEventArgs e) => ShowExtractionQueue(selected, ActiveArchive);
    private void ShowExtractionQueue(Game? game, GameArchive? archive) => Guard(() =>
    {
        if (operation is not null || extractionQueue is null) return;
        Commit();
        new ArchiveQueueWindow(extractionQueue, game, archive, job =>
        {
            var updated = ExtractionQueue.Link(library, job);
            store.Save(updated);
            Games.SelectedItem = null; selected = null; library = updated;
            return Task.CompletedTask;
        }) { Owner = this }.ShowDialog();
        if (detailsPage && selected is null) BackToCollection(this, new());
        else Refresh();
    });
}
