namespace ArcHive.Core;
public sealed record PersonalReview(double? Stars=null,string Text="",DateTimeOffset? CreatedAt=null,DateTimeOffset? UpdatedAt=null)
{
    public PersonalReview Edit(double? stars,string text,DateTimeOffset now)
    {
        if(stars is double s&&(!double.IsFinite(s)||s<0.5||s>5||s*2!=Math.Truncate(s*2)))throw new ArgumentException("Choose 0.5 to 5 stars in half-star steps, or Unrated.");
        if(text.Length>20000)throw new ArgumentException("Keep your review within 20,000 characters.");
        if(stars==Stars&&text==Text)return this;
        return new(stars,text,CreatedAt??now,now);
    }
    public string Summary => Stars is double s ? $"Your rating: {s:0.0} / 5" : "Your rating: Unrated";
}
