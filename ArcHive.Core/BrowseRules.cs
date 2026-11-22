namespace ArcHive.Core;
public enum TagOrder { Name, MostUsed, FavoritesFirst }
public sealed record TagCount(string Name,int Count,bool Favorite,bool Blacklisted);
public sealed class BrowseRules
{
    public List<string> FavoriteTags { get; set; } = [];
    public List<string> BlacklistedTags { get; set; } = [];
    public List<Guid> BlacklistedGames { get; set; } = [];
    public TagOrder TagOrder { get; set; } = TagOrder.FavoritesFirst;
    public bool Blocks(Game game) => BlacklistedGames.Contains(game.Id)||GameTags.Parse(game.Tags).Any(t=>BlacklistedTags.Contains(t,StringComparer.OrdinalIgnoreCase));
    public static void Toggle(List<string> list,string tag)
    {
        tag=tag.Trim();if(tag.Length==0)return;
        if(list.Contains(tag,StringComparer.OrdinalIgnoreCase))list.RemoveAll(t=>string.Equals(t,tag,StringComparison.OrdinalIgnoreCase));else list.Add(tag);
    }
    public IEnumerable<TagCount> Tags(IEnumerable<Game> games,string search="",bool favoritesOnly=false)
    {
        var counts=games.SelectMany(g=>GameTags.Parse(g.Tags)).GroupBy(t=>t,StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.Count(),StringComparer.OrdinalIgnoreCase);
        var rows=counts.Keys.Concat(FavoriteTags).Concat(BlacklistedTags).Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(t=>t.Contains(search.Trim(),StringComparison.OrdinalIgnoreCase))
            .Select(t=>new TagCount(t,counts.GetValueOrDefault(t),FavoriteTags.Contains(t,StringComparer.OrdinalIgnoreCase),BlacklistedTags.Contains(t,StringComparer.OrdinalIgnoreCase)))
            .Where(t=>!favoritesOnly||t.Favorite);
        return rows.OrderByDescending(t=>TagOrder==TagOrder.FavoritesFirst&&t.Favorite).ThenByDescending(t=>TagOrder==TagOrder.MostUsed?t.Count:0).ThenBy(t=>t.Name,StringComparer.OrdinalIgnoreCase);
    }
}
