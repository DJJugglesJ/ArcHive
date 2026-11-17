using System.Windows;
using System.Windows.Controls;
using ArcHive.Core;
namespace ArcHive.App;
public partial class MainWindow
{
    private string? activeTag;
    private Button TagButton(string tag, string label)
    {
        var button=new Button { Content=label, Tag=tag, Padding=new Thickness(12,6,12,6), Margin=new Thickness(0,0,6,6), ToolTip="Show games tagged " + tag };
        button.Click+=(_,_)=> { Commit(); activeTag=tag; if(detailsPage) BackToCollection(this,new()); else Refresh(); FocusSelectedEntry(); };
        return button;
    }
    private void ShowDetailTags()
    {
        DetailTagButtons.Children.Clear(); var tags=GameTags.Parse(selected?.Tags);
        NoTagsHint.Visibility=tags.Length==0 ? Visibility.Visible : Visibility.Collapsed;
        foreach(var tag in tags) DetailTagButtons.Children.Add(TagButton(tag,tag));
    }
    private void ShowTagFilters()
    {
        LibraryTagButtons.Children.Clear(); if(activeTag is not null) TagBrowser.IsExpanded=true; ActiveTagButton.Content=activeTag is null ? "All tags" : "Clear tag: " + activeTag;
        foreach(var group in library.Games.Where(preferences.Includes).SelectMany(g=>GameTags.Parse(g.Tags)).GroupBy(t=>t,StringComparer.OrdinalIgnoreCase).OrderBy(g=>g.Key,StringComparer.OrdinalIgnoreCase))
            LibraryTagButtons.Children.Add(TagButton(group.Key,$"{group.Key} ({group.Count()})"));
    }
    private void ClearTagFilter(object sender,RoutedEventArgs e) { Commit(); activeTag=null; Refresh(); FocusSelectedEntry(); }
    private void EditTags(object sender,RoutedEventArgs e)
    {
        Commit(); if(selected is null) return;
        var editor=new TagEditorWindow(selected.Tags) { Owner=this };
        if(editor.ShowDialog()!=true) return;
        TagsBox.Text=editor.Result; SaveDetails(sender,e); if(selected is null && detailsPage) BackToCollection(this,new()); else ShowDetailTags();
    }
}
