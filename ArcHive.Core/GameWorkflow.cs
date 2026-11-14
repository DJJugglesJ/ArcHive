namespace ArcHive.Core;
public enum GamePrimaryAction { Play, ConfigureLaunch, Extract, LinkFolder }
public static class GameWorkflow
{
    public static GamePrimaryAction Primary(GameInstallation? installation, LaunchProfile? profile, GameArchive? archive)
    {
        if (installation is not null && profile is not null && LaunchService.CanLaunch(installation, profile)) return GamePrimaryAction.Play;
        if (installation is not null && Directory.Exists(installation.Path)) return GamePrimaryAction.ConfigureLaunch;
        if (archive is not null && File.Exists(archive.Path)) return GamePrimaryAction.Extract;
        return GamePrimaryAction.LinkFolder;
    }
    public static string Label(GamePrimaryAction action) => action switch { GamePrimaryAction.Play => "Play game", GamePrimaryAction.ConfigureLaunch => "Set up launch", GamePrimaryAction.Extract => "Extract to play", _ => "Link game folder" };
}
