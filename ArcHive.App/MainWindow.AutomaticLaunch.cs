using ArcHive.Core;
namespace ArcHive.App;
public partial class MainWindow
{
    private async void ConfigureAutomaticLaunch()
    {
        var game=selected; var installation=ActiveInstallation;
        if (game is null || installation is null || !AutomaticLaunch.IsUntouched(installation)) return;
        var path=await Task.Run(()=>AutomaticLaunch.Find(installation.Path));
        if (path is null || selected!=game || operation is not null) return;
        try
        {
            CommitLocation();
            if (!AutomaticLaunch.Apply(installation,path)) return;
            game.DetectionHistory.Add(new(DateTimeOffset.UtcNow,installation.Path,path,"",["One non-helper top-level executable; cleaned name matches its folder. Automatic launch profile; editable in Launch Options."]));
            LoadLocationEditors(); UpdateLocationSummary(); DetailState.Text=$"Installation Status: {game.InstallationStatus} · Play Status: {game.Progress} · Update Status: {game.UpdateStatus}"; store.Save(library);
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or InvalidOperationException)
        { Status.Text="Automatic launch setup could not be saved. Use Launch Options."; }
    }
}
