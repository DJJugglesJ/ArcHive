using ArcHive.Core;
using System.Text.Json;
internal static class BrowseRulesTests
{
 public static void Run(string root,Action<bool,string> check)
 {
  var a=new Game{Title="Same",Tags="Action, Puzzle, ACTION",Favorite=true};var b=new Game{Title="Same",Tags="Puzzle, Co-op"};var c=new Game{Title="Hidden",Tags="Adult",IsNsfw=true};var games=new[]{a,b,c};var p=new AppPreferences();
  check(p.Includes(a)&&!p.Includes(c),"Default browse rules preserve existing visibility");
  BrowseRules.Toggle(p.BrowseRules.FavoriteTags,"co-OP");p.BrowseRules.TagOrder=TagOrder.FavoritesFirst;
  check(p.BrowseRules.Tags(games).First().Name=="Co-op","Favorite tags sort first case insensitively");
  check(p.BrowseRules.Tags(games,"PUZ").Single().Count==2,"Tag search is case insensitive and counts distinct games");
  check(p.BrowseRules.Tags(games,"",true).Single().Name=="Co-op","Favorite tag filter excludes other tags");
  p.BrowseRules.TagOrder=TagOrder.MostUsed;check(p.BrowseRules.Tags(games).First().Name=="Puzzle","Most-used tags sort by game count");
  p.BrowseRules.TagOrder=TagOrder.Name;check(p.BrowseRules.Tags(games).First().Name=="Action","Alphabetical tag ordering");
  BrowseRules.Toggle(p.BrowseRules.BlacklistedTags,"action");check(!p.Includes(a)&&p.Includes(b)&&a.Favorite,"Blacklisted tags hide games without clearing favorites");
  a.Tags="Actionable";check(p.Includes(a),"Blacklist matches whole tags rather than substrings");
  p.BrowseRules.BlacklistedGames.Add(a.Id);check(!p.Includes(a)&&p.Includes(b),"Game blacklist uses ID not duplicate title");
  p.BrowseRules.BlacklistedGames.Remove(a.Id);check(p.Includes(a),"Restoring game removes explicit blacklist");
  BrowseRules.Toggle(p.BrowseRules.BlacklistedTags,"ACTION");check(p.BrowseRules.BlacklistedTags.Count==0,"Restore tag is case insensitive");
  var file=Path.Combine(root,"browse-preferences.json");var store=new AppPreferencesStore(file);store.Save(p);var loaded=store.Load();check(loaded.BrowseRules.FavoriteTags.Single()=="co-OP"&&loaded.BrowseRules.TagOrder==TagOrder.Name,"Tag choices persist across restart");
  var draft=JsonSerializer.Deserialize<AppPreferences>(JsonSerializer.Serialize(p))!;draft.BrowseRules.BlacklistedGames.Add(b.Id);check(p.Includes(b),"Cancelled draft cannot modify live blacklist");
  File.WriteAllText(file,"{\"ShowNsfw\":true}");check(store.Load().Includes(c),"Older settings gain empty browsing rules without changing existing values");
  var db=new SqliteLibraryStore(Path.Combine(root,"browse-library.db"));db.Load();db.Save(new(){Games=games.ToList()});check(db.Load().Games.Single(g=>g.Id==a.Id).Favorite,"Game favorites survive SQLite round trip");
 }
}
