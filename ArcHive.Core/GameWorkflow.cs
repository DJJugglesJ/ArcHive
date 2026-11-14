namespace ArcHive.Core;
public enum GamePrimaryAction { Play, ConfigureLaunch, Extract, LinkFolder }
public static class GameWorkflow
{
    public static GamePrimaryAction Primary(GameInstallation? installation, LaunchProfile? profile, GameArchive? archive)
    {
        if (installation is not null && profile is not null && LaunchService.CanConfigure(installation, profile)) return GamePrimaryAction.Play;
        if (installation is not null && Directory.Exists(installation.Path)) return GamePrimaryAction.ConfigureLaunch;
        if (archive is not null && File.Exists(archive.Path)) return GamePrimaryAction.Extract;
        return GamePrimaryAction.LinkFolder;
    }
    public static string Label(GamePrimaryAction action) => action switch { GamePrimaryAction.Play => "Play Game", GamePrimaryAction.ConfigureLaunch => "Set Up Launch…", GamePrimaryAction.Extract => "Extract to Play…", _ => "Link Game Folder…" };
}
