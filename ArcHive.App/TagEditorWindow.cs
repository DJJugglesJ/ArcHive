using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArcHive.Core;
namespace ArcHive.App;
public sealed class TagEditorWindow : Window
{
    public string Result { get; private set; }
    public TagEditorWindow(string original)
    {
        Result=original; Title="Edit Tags"; Width=600; Height=430; WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Background=new SolidColorBrush(Color.FromRgb(8,13,11)); Foreground=Brushes.White;
        var tags=GameTags.Parse(original).ToList(); var panel=new DockPanel { Margin=new Thickness(24) }; Content=panel;
        var heading=new TextBlock { Text="Your tags", FontSize=24, Margin=new Thickness(0,0,0,12) }; DockPanel.SetDock(heading,Dock.Top);panel.Children.Add(heading);
        var input=new TextBox { ToolTip="Add tags, separated by commas", Margin=new Thickness(0,0,0,8) };System.Windows.Automation.AutomationProperties.SetName(input,"New tags, separated by commas");
        var top=new StackPanel();DockPanel.SetDock(top,Dock.Top);panel.Children.Add(top);top.Children.Add(input);
        var chips=new WrapPanel();
        void Render() { chips.Children.Clear();foreach(var tag in tags.ToArray()){var b=new Button{Content=tag+"  ×",ToolTip="Remove tag "+tag};b.Click+=(_,_)=>{tags.Remove(tag);Render();input.Focus();};chips.Children.Add(b);} }
        void Add() { tags=GameTags.Parse(string.Join(",",tags)+","+input.Text).ToList();input.Clear();Render();input.Focus(); }
        var add=new Button{Content="Add Tags",HorizontalAlignment=HorizontalAlignment.Left};add.Click+=(_,_)=>Add();top.Children.Add(add);
        var help=new TextBlock{Text="Select a tag below to remove it. Changes apply when you save.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,12)};top.Children.Add(help);
        var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};DockPanel.SetDock(actions,Dock.Bottom);panel.Children.Add(actions);
        var save=new Button{Content="Save Tags"};save.Click+=(_,_)=>{Add();Result=string.Join(", ",tags);DialogResult=true;};actions.Children.Add(save);actions.Children.Add(new Button{Content="Cancel",IsCancel=true});
        panel.Children.Add(new ScrollViewer{Content=chips,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});Render();Loaded+=(_,_)=>input.Focus();
    }
}
