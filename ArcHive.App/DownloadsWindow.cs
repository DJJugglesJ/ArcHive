using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArcHive.Core;
namespace ArcHive.App;
public sealed class DownloadsWindow : Window
{
    public string? ReviewedFile { get; private set; }
    public DownloadsWindow(DownloadQueue queue)
    {
        Title="Downloads";Width=780;Height=640;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Background=new SolidColorBrush(Color.FromRgb(8,13,11));Foreground=Brushes.White;
        var panel=new DockPanel{Margin=new Thickness(24)};Content=panel;
        var top=new StackPanel();DockPanel.SetDock(top,Dock.Top);panel.Children.Add(top);
        top.Children.Add(new TextBlock{Text="Download, then review",FontSize=24});
        top.Children.Add(new TextBlock{Text="Public HTTPS archive links only. Login pages, temporary signed links and checkout stay in your browser. Downloads never run automatically.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,12)});
        var url=new TextBox{ToolTip="Direct HTTPS archive URL"};System.Windows.Automation.AutomationProperties.SetName(url,"Direct archive URL");top.Children.Add(new TextBlock{Text="Direct file link"});top.Children.Add(url);
        var name=new TextBox{ToolTip="Filename ending in .zip, .rar or .7z"};System.Windows.Automation.AutomationProperties.SetName(name,"Archive filename");top.Children.Add(new TextBlock{Text="Save as"});top.Children.Add(name);
        var add=new Button{Content="Add to Queue"};top.Children.Add(add);
        var status=new TextBlock{Text="Interrupted downloads restart from the beginning. Failed links may require downloading through your browser.",TextWrapping=TextWrapping.Wrap};DockPanel.SetDock(status,Dock.Bottom);panel.Children.Add(status);
        var actions=new WrapPanel();DockPanel.SetDock(actions,Dock.Bottom);panel.Children.Add(actions);
        var start=new Button{Content="Start / Retry Queue"};var stop=new Button{Content="Stop",IsEnabled=false};var review=new Button{Content="Add to Library & Review"};var close=new Button{Content="Close",IsCancel=true};
        foreach(var b in new[]{start,stop,review,close})actions.Children.Add(b);
        var list=new ListBox();System.Windows.Automation.AutomationProperties.SetName(list,"Download queue");panel.Children.Add(list);
        void Refresh(){var chosen=list.SelectedItem;list.ItemsSource=null;list.ItemsSource=queue.Jobs;list.SelectedItem=chosen;}
        CancellationTokenSource? pending=null;
        add.Click+=(_,_)=>{try{queue.Add(url.Text.Trim(),name.Text.Trim());url.Clear();name.Clear();Refresh();}catch(Exception ex){status.Text=ex.Message;}};
        start.Click+=async (_,_)=>{
            pending=new();add.IsEnabled=start.IsEnabled=review.IsEnabled=false;stop.IsEnabled=true;
            try{using var client=DownloadQueue.CreateClient();await queue.RunAsync(client,new Progress<long>(n=>status.Text=$"Downloading · {n/1048576d:0.0} MB"),pending.Token);status.Text="Queue stopped or finished. Select a ready archive to review before extracting.";}
            catch{status.Text="Could not update the download queue. Existing files were retained.";}
            finally{pending.Dispose();pending=null;add.IsEnabled=start.IsEnabled=review.IsEnabled=true;stop.IsEnabled=false;Refresh();}
        };
        stop.Click+=(_,_)=>pending?.Cancel();
        review.Click+=async (_,_)=>{
            if(pending is not null||list.SelectedItem is not DownloadJob j)return;
            pending=new();add.IsEnabled=start.IsEnabled=review.IsEnabled=false;stop.IsEnabled=true;
            string? verified=null;
            try{status.Text="Verifying archive before review...";verified=await queue.ReviewAsync(j,pending.Token);}
            catch(OperationCanceledException){status.Text="Review stopped. No library changes made.";}
            catch(Exception ex){status.Text=ex.Message;}
            finally{pending.Dispose();pending=null;add.IsEnabled=start.IsEnabled=review.IsEnabled=true;stop.IsEnabled=false;}
            if(verified is not null){ReviewedFile=verified;DialogResult=true;}
        };
        Closing+=(_,e)=>{if(pending is not null){pending.Cancel();e.Cancel=true;status.Text="Stopping download. Close again when the queue has stopped.";}};
        Refresh();Loaded+=(_,_)=>url.Focus();
    }
}

