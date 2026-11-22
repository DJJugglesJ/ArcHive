using ArcHive.Core;
internal static class SmartNamingTests
{
    public static void Run(string root, Action<bool,string> check)
    {
        var parsed=FilenameParser.Parse("XMLQuest.2_v1.2-beta3_build_0042_Win64_[group-Example].7z");
        check(parsed.Title=="XML Quest 2" && parsed.Version=="1.2-beta3" && parsed.Build=="0042" && parsed.Platform=="Windows x64" && parsed.ReleaseGroup=="Example", "Smart naming separates explicit release/build/platform/group tokens");
        check(parsed.Original.EndsWith(".7z") && parsed.Evidence.Length==4,"Smart naming retains original and token evidence");
        check(FilenameParser.Parse("Game_[Developer].zip").Title=="Game [Developer]","Unlabelled bracket text is preserved");
        check(FilenameParser.Parse("Game_[group-Win64 v2].zip").Platform is null,"Release-group text is not misclassified as platform or version");
        check(FilenameParser.Parse("Game_v2026-11-22.zip").Version=="2026-11-22","Explicit date versions retain their raw format");
        check(FilenameParser.Parse("Game_1984.zip").Title=="Game 1984","Bare numbers remain title text");
        check(NamingTemplates.Render("{title} - {version}","Manual","Game_v1.zip",new(){Version="custom-beta"})=="Manual - custom-beta","Templates prefer reviewed release values");
        foreach(var template in new[]{"{title}/{version}","{unknown}{title}","{title}.","{title}{build}","../escape","{title}{"})
        {try{NamingTemplates.Render(template,"Game","Game.zip",new(){Version="1"});check(false,"Unsafe or incomplete template accepted");}catch(Exception e) when(e is ArgumentException or IOException){check(true,"Unsafe or incomplete template rejected");}}
        var dir=Path.Combine(root,"smart-naming");Directory.CreateDirectory(dir);
        var first=Path.Combine(dir,"Original_v1.zip");var second=Path.Combine(dir,"Original_v2.zip");File.WriteAllText(first,"one");File.WriteAllText(second,"two");
        var game=new Game{Title="Manual Title",Naming=new(){CanonicalTitle="Canonical",OriginalName="Original"},PersonalReview=new(4,"Keep me"),Archives=[new(){Path=first,Release=new(){Version="1"}},new(){Path=second,Release=new(){Version="2"}}]};
        var library=new Library{Games=[game]};
        var plan=LibraryFileMoves.Preview(library,[game.Id],null,true,"{title} - {version}");
        check(plan.Moves.Select(m=>Path.GetFileName(m.Destination)).SequenceEqual(new[]{"Canonical - 1.zip","Canonical - 2.zip"}) && File.Exists(first),"Template preview separates versions without moving originals");
        try{LibraryFileMoves.Preview(library,[game.Id],null,true);check(false,"Duplicate destinations accepted");}catch(IOException){check(true,"Template collisions rejected before mutation");}
        var result=LibraryFileMoves.Apply(library,plan,Path.Combine(dir,"journal.json"),_=>{});
        check(result.Games[0].Id==game.Id && result.Games[0].Title==game.Title && result.Games[0].PersonalReview==game.PersonalReview && result.Games[0].Naming.OriginalName=="Original" && result.Games[0].Archives.All(a=>File.Exists(a.Path)),"Template rename preserves identity, manual title, original naming and reviews");
    }
}
