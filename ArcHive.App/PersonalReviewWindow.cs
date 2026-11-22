using System.Windows;
using System.Windows.Controls;
using ArcHive.Core;
namespace ArcHive.App;
public sealed class PersonalReviewWindow:ToolWindow
{
    public PersonalReview Result { get; private set; }
    public PersonalReviewWindow(PersonalReview original):base("Your Rating & Review")
    {
        Result=original;Label("Your own rating and notes, separate from source scores. Stored locally in your library.");
        var values=new[]{"Unrated"}.Concat(Enumerable.Range(1,10).Select(i=>$"{i/2d:0.0} / 5")).ToArray();
        var rating=Options("Your Rating",values,values[original.Stars is double s?(int)(s*2):0]);
        var review=Input("Your Review",original.Text);review.AcceptsReturn=true;review.TextWrapping=TextWrapping.Wrap;review.Height=240;review.MaxLength=20000;review.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;
        Label(original.UpdatedAt is {} date?$"Last edited: {date.LocalDateTime:g}":"No personal review saved.");
        ActionButton("Save Review",()=>{Result=original.Edit(rating.SelectedIndex==0?null:rating.SelectedIndex/2d,review.Text,DateTimeOffset.UtcNow);DialogResult=true;});
        ActionButton("Cancel",()=>DialogResult=false);Body.Children.Add(StatusText);
    }
}
public partial class MainWindow
{
    private void EditPersonalReview(object sender,RoutedEventArgs e)=>Guard(()=>
    {
        if(selected is null||operation is not null)return;Commit();var game=selected;
        var dialog=new PersonalReviewWindow(game.PersonalReview){Owner=this};if(dialog.ShowDialog()!=true)return;
        var previous=game.PersonalReview;game.PersonalReview=dialog.Result;
        try{store.Save(library);}catch{game.PersonalReview=previous;throw;}
        Refresh();if(selected is null&&detailsPage)BackToCollection(this,new());else ShowDetailTags();
    });
}
