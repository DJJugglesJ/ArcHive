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
        PersonalReviewButton.Content=(selected?.PersonalReview.Summary ?? "Your Rating") + " · Review…";
        DetailTagButtons.Children.Clear(); var tags=GameTags.Parse(selected?.Tags);
        NoTagsHint.Visibility=tags.Length==0 ? Visibility.Visible : Visibility.Collapsed;
        foreach(var tag in tags) DetailTagButtons.Children.Add(TagButton(tag,tag));
    }
    private void ShowTagFilters()
    {
        LibraryTagButtons.Children.Clear(); if(activeTag is not null) TagBrowser.IsExpanded=true; ActiveTagButton.Content=activeTag is null ? "All tags" : "Clear tag: " + activeTag;
        foreach(var tag in preferences.BrowseRules.Tags(library.Games.Where(preferences.Includes).ToArray(),TagSearch.Text,FavoriteTagsOnly.IsChecked==true).Where(t=>!t.Blacklisted&&t.Count>0))
            LibraryTagButtons.Children.Add(TagButton(tag.Name,$"{(tag.Favorite ? "★ " : "")}{tag.Name} ({tag.Count})"));
    }
    private void TagSearchChanged(object sender,RoutedEventArgs e) { if(ready)ShowTagFilters(); }
    private void ManageBrowseRules(object sender,RoutedEventArgs e)=>Guard(()=>
    {
        if(operation is not null)return;
        Commit();var dialog=new BrowseRulesWindow(preferences,library.Games){Owner=this};
        if(dialog.ShowDialog()!=true)return;
        var updated=System.Text.Json.JsonSerializer.Deserialize<Library>(System.Text.Json.JsonSerializer.Serialize(library))!;
        foreach(var game in updated.Games)if(dialog.Favorites.TryGetValue(game.Id,out var favorite))game.Favorite=favorite;
        store.Save(updated);library=updated;Games.SelectedItem=null;selected=null;
        preferenceStore.Save(dialog.Result);preferences=dialog.Result;
        if(detailsPage)BackToCollection(this,new());else Refresh();
    });
    private void ClearTagFilter(object sender,RoutedEventArgs e) { Commit(); activeTag=null; Refresh(); FocusSelectedEntry(); }
    private void EditTags(object sender,RoutedEventArgs e)
    {
        Commit(); if(selected is null) return;
        var editor=new TagEditorWindow(selected.Tags) { Owner=this };
        if(editor.ShowDialog()!=true) return;
        TagsBox.Text=editor.Result; SaveDetails(sender,e); if(selected is null && detailsPage) BackToCollection(this,new()); else ShowDetailTags();
    }
}
