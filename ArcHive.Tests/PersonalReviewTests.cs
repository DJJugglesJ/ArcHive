using ArcHive.Core;
using System.Text.Json;
internal static class PersonalReviewTests
{
 public static void Run(string root,Action<bool,string> check)
 {
  var now=DateTimeOffset.UtcNow;var review=new PersonalReview().Edit(4.5,"My review",now);
  check(review.Stars==4.5&&review.CreatedAt==now&&review.UpdatedAt==now,"Personal rating stores half stars and timestamps");
  check(review.Edit(4.5,"My review",now.AddDays(1))==review,"Unchanged review preserves timestamps");
  var edited=review.Edit(null,"Notes without rating",now.AddDays(1));check(edited.CreatedAt==now&&edited.UpdatedAt>now&&edited.Stars is null,"Unrating retains creation history and permits notes");
  foreach(var bad in new[]{0d,5.5,1.3,double.NaN,double.PositiveInfinity}){try{review.Edit(bad,"",now);check(false,"Invalid rating accepted");}catch(ArgumentException){check(true,"Invalid rating rejected");}}
  try{review.Edit(1,new string('x',20001),now);check(false,"Long review accepted");}catch(ArgumentException){check(true,"Review size is bounded");}
  var a=new Game{Title="A",PersonalReview=review};var b=new Game{Title="B"};var c=new Game{Title="C",PersonalReview=new PersonalReview().Edit(2,"",now)};var games=new[]{a,b,c};
  check(LibraryBrowsing.Apply(games,new(Sort:"Your Rating")).SequenceEqual(new[]{a,c,b}),"Personal rating sort puts unrated games last");
  check(LibraryBrowsing.Apply(games,new(Rating:"Unrated")).Single()==b,"Unrated filter");
  check(LibraryBrowsing.Apply(games,new(Rating:"Rated")).Count()==2,"Rated filter");
  check(LibraryBrowsing.Apply(games,new(Rating:"4 Stars & Up")).Single()==a,"High rating filter");
  var db=new SqliteLibraryStore(Path.Combine(root,"personal-reviews.db"));db.Load();db.Save(new(){Games=games.ToList()});var loaded=db.Load();check(loaded.Games.Single(g=>g.Id==a.Id).PersonalReview==review,"SQLite persists personal reviews independently of provider metadata");
  var legacy=JsonSerializer.Deserialize<Game>("{\"Title\":\"Legacy\"}")!;check(legacy.PersonalReview.Stars is null&&legacy.PersonalReview.Text=="","Legacy games default to unrated");
  var candidate=new MetadataCandidate("fixture","1","A",[],new(){[MetadataField.Description]="Source description",[MetadataField.Tags]="Puzzle"},now);
  MetadataReconciliation.Apply(a,candidate,MetadataReconciliation.Preview(a,candidate));
  check(a.PersonalReview==review,"Provider updates preserve personal rating and review");
  MetadataReconciliation.Undo(a);check(a.PersonalReview==review,"Metadata undo preserves personal review");
  var folder=Path.Combine(root,"ReviewGame");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"ReviewGame.exe"),"fixture");
  var scanned=Scanner.Scan(folder,default).Games.ToList();scanned.Single().PersonalReview=review;
  var rescanned=new Library{Games=scanned};Scanner.Merge(rescanned,Scanner.Scan(folder,default).Games);
  check(rescanned.Games.Count==1&&rescanned.Games[0].PersonalReview==review,"Rescanning preserves personal rating and review");
  var merged=DuplicateMerge.Preview(new(){Games=games.ToList()},new[]{a.Id,c.Id},a.Id);check(merged.Result.Games.Single(g=>g.Id==a.Id).PersonalReview==review&&merged.Result.Games.Single(g=>g.Id==a.Id).MergedRecords.Single().PersonalReview==c.PersonalReview,"Merge retains primary review and conflicting source review");
 }
}
