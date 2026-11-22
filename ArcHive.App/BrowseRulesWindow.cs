using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArcHive.Core;
namespace ArcHive.App;
public sealed class BrowseRulesWindow:Window
{
    public AppPreferences Result { get; }
    public Dictionary<Guid,bool> Favorites { get; } = [];
    public BrowseRulesWindow(AppPreferences source,IEnumerable<Game> games)
    {
        Result=JsonSerializer.Deserialize<AppPreferences>(JsonSerializer.Serialize(source))!;
        var visible=games.Where(g=>source.ShowNsfw||!g.IsNsfw).ToArray();
        foreach(var game in visible)Favorites[game.Id]=game.Favorite;
        Title="Tags & Blacklists";Width=760;Height=620;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Background=new SolidColorBrush(Color.FromRgb(8,13,11));Foreground=Brushes.White;
        var panel=new DockPanel{Margin=new Thickness(24)};Content=panel;
        var top=new StackPanel();DockPanel.SetDock(top,Dock.Top);panel.Children.Add(top);
        top.Children.Add(new TextBlock{Text="Make browsing yours",FontSize=24});
        top.Children.Add(new TextBlock{Text="Blacklists hide games from browsing; files and metadata stay intact. Restore them here. Favorite tags and games here, or restore blacklisted entries.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,10)});
        var mode=new ComboBox{ItemsSource=new[]{"Tags","Games"},SelectedIndex=0};top.Children.Add(mode);
        var search=new TextBox{ToolTip="Search tags or game titles"};System.Windows.Automation.AutomationProperties.SetName(search,"Search tags or games");top.Children.Add(search);
        var order=new ComboBox{ItemsSource=new[]{"Alphabetical","Most used","Favorites first"},SelectedIndex=(int)Result.BrowseRules.TagOrder};System.Windows.Automation.AutomationProperties.SetName(order,"Tag sort order");top.Children.Add(order);
        var actions=new WrapPanel();DockPanel.SetDock(actions,Dock.Bottom);panel.Children.Add(actions);
        var favorite=new Button{Content="Toggle Favorite"};var block=new Button{Content="Blacklist / Restore"};var save=new Button{Content="Save Changes"};var cancel=new Button{Content="Cancel",IsCancel=true};
        foreach(var b in new[]{favorite,block,save,cancel})actions.Children.Add(b);
        var list=new ListBox();System.Windows.Automation.AutomationProperties.SetName(list,"Tags and blacklist entries");panel.Children.Add(list);
        void Refresh(){var previous=(list.SelectedItem as Row)?.Key;var rules=Result.BrowseRules;
            list.ItemsSource=mode.SelectedIndex==0?rules.Tags(visible,search.Text).Select(t=>new Row(t.Name,t.Name+$" ({t.Count})"+(t.Favorite?" · Favorite":"")+(t.Blacklisted?" · Blacklisted":""))).ToArray():visible.Where(g=>g.Title.Contains(search.Text.Trim(),StringComparison.OrdinalIgnoreCase)).OrderBy(g=>g.Title).Select(g=>new Row(g.Id.ToString(),g.Title+(Favorites[g.Id]?" · Favorite":"")+(rules.BlacklistedGames.Contains(g.Id)?" · Blacklisted":rules.Blocks(g)?" · Hidden by tag":""))).ToArray();
            list.SelectedItem=list.Items.Cast<Row>().FirstOrDefault(r=>r.Key==previous);order.IsEnabled=mode.SelectedIndex==0;
        }
        mode.SelectionChanged+=(_,_)=>Refresh();search.TextChanged+=(_,_)=>Refresh();order.SelectionChanged+=(_,_)=>{if(order.SelectedIndex>=0)Result.BrowseRules.TagOrder=(TagOrder)order.SelectedIndex;Refresh();};
        favorite.Click+=(_,_)=>{if(list.SelectedItem is Row row){if(mode.SelectedIndex==0)BrowseRules.Toggle(Result.BrowseRules.FavoriteTags,row.Key);else {var id=Guid.Parse(row.Key);Favorites[id]=!Favorites[id];}Refresh();list.Focus();}};
        block.Click+=(_,_)=>{if(list.SelectedItem is not Row row)return;if(mode.SelectedIndex==0)BrowseRules.Toggle(Result.BrowseRules.BlacklistedTags,row.Key);else{var id=Guid.Parse(row.Key);if(!Result.BrowseRules.BlacklistedGames.Remove(id))Result.BrowseRules.BlacklistedGames.Add(id);}Refresh();list.Focus();};
        save.Click+=(_,_)=>DialogResult=true;Refresh();Loaded+=(_,_)=>search.Focus();
    }
    private sealed record Row(string Key,string Label){public override string ToString()=>Label;}
}
