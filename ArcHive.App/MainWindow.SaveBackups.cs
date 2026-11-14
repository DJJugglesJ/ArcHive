using System.IO;
using System.Text.Json;
using System.Windows;
using ArcHive.Core;
namespace ArcHive.App;
public partial class MainWindow
{
    private void OpenSaveBackups(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (selected is null || operation is not null) return;
        Commit(); var game = selected;
        new SaveBackupsWindow(game, Path.Combine(data, "save-backups"), locations =>
        {
            var updated = JsonSerializer.Deserialize<Library>(JsonSerializer.Serialize(library))!;
            updated.Games.Single(g => g.Id == game.Id).SaveLocations = locations;
            store.Save(updated); game.SaveLocations = locations;
        }) { Owner = this }.ShowDialog();
    });
}
