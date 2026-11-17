using ArcHive.Core;
internal static class TagLaunchTests
{
    public static void Run(string root, Action<bool,string> check)
    {
        check(GameTags.Normalize(" Puzzle, co-op;PUZZLE\n Story ")=="co-op, Puzzle, Story","Tags normalize separators, spacing and duplicates");
        check(GameTags.Contains("Action, Cooperative", "ACTION") && !GameTags.Contains("Action, Cooperative","Coop"),"Tag filtering matches whole tags without substring collisions");
        var html="<meta property='og:url' content='https://f95zone.to/threads/123/'><title>Example [v1.0]</title><a href='/tags/co-op/'>Co-op</a><a class='tagItem'>Puzzle</a><article><a href='/tags/unrelated/'>Reply tag</a></article>";
        var result=F95ThreadSource.Parse("https://f95zone.to/threads/123/",html);
        check(result.Candidate.Fields[MetadataField.Tags].Contains("Co-op") && !result.Candidate.Fields[MetadataField.Tags].Contains("Reply"),"F95 header tag links parse without importing reply tags");
        var folder=Path.Combine(root,"Silver_Moon_v1.2_win64");Directory.CreateDirectory(folder);var exe=Path.Combine(folder,"SilverMoon.exe");File.WriteAllText(exe,"fixture, never executed");
        var installation=new GameInstallation{Path=folder};
        check(AutomaticLaunch.Find(folder)==exe,"Automatic launch matches CamelCase executable to versioned folder");
        check(AutomaticLaunch.Apply(installation,exe) && LaunchService.CanLaunch(installation,installation.Profiles.Single()),"Matching executable creates a runnable default profile");
        installation.Profiles[0].Arguments.Add("--manual");
        check(!AutomaticLaunch.Apply(installation,exe) && installation.Profiles[0].Arguments.SequenceEqual(["--manual"]),"Automatic launch preserves existing profiles and arguments");
        File.WriteAllText(Path.Combine(folder,"setup.exe"),"helper");check(AutomaticLaunch.Find(folder)==exe,"Known installer helper does not obscure the sole game executable");
        var other=Path.Combine(folder,"OtherGame.exe");File.WriteAllText(other,"fixture");check(AutomaticLaunch.Find(folder) is null,"Multiple game executables require review");File.Delete(other);
        var mismatch=Path.Combine(root,"DifferentGame");Directory.CreateDirectory(mismatch);File.WriteAllText(Path.Combine(mismatch,"SilverMoon.exe"),"fixture");check(AutomaticLaunch.Find(mismatch) is null,"Unrelated sole executable is not selected automatically");
        var customized=new GameInstallation{Path=folder,Profiles=[new LaunchProfile{Name="My setup"}]};check(!AutomaticLaunch.Apply(customized,exe),"Even an empty custom-named profile is preserved");
        File.Delete(exe);check(AutomaticLaunch.Find(folder) is null,"Helper-only folder does not become runnable");
    }
}
