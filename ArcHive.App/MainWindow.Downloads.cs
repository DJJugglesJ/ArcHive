using System.IO;
using System.Windows;
using ArcHive.Core;
namespace ArcHive.App;
public partial class MainWindow
{
    private void OpenDownloads(object sender,RoutedEventArgs e)=>Guard(()=>
    {
        if(operation is not null)return;
        Commit();
        using var queue=new DownloadQueue(Path.Combine(data,"downloads"));
        var window=new DownloadsWindow(queue){Owner=this};
        if(window.ShowDialog()!=true||window.ReviewedFile is not string path)return;
        var game=library.Games.FirstOrDefault(g=>g.Archives.Any(a=>Game.SamePath(a.Path,path)));
        if(game is null){game=new Game{Title=FilenameParser.FromLocation(path,false).Title};game.Archives.Add(new(){Path=path,Release=ReleaseInfo.FromPath(path)});library.Games.Add(game);store.Save(library);}
        Refresh();ShowExtractionQueue(game,game.Archives.First(a=>Game.SamePath(a.Path,path)));
    });
}
