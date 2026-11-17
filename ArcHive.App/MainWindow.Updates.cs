using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using ArcHive.Core;
namespace ArcHive.App;
public partial class MainWindow
{
    private async void CheckStartupUpdate()
    {
        if (!preferences.CheckForUpdatesOnStartup) return;
        try
        {
            using var client=new HttpClient(new HttpClientHandler { AllowAutoRedirect=false, UseCookies=false });
            var release=await new AppUpdates(client).CheckAsync(default);
            if (IsLoaded && release.Version>AppUpdates.Current && operation is null)
                Status.Text=$"ArcHive {release.Version} is available. Open Settings > Updates to review and install.";
        }
        catch { /* Optional background check never blocks the library. Manual check provides retry. */ }
    }
    private async void InstallAppUpdate(string? path, AppRelease? release)
    {
        try
        {
            if (path is null || release is null || release.Version<=AppUpdates.Current) return;
            if (operation is not null || library.Games.Any(g=>g.Sessions.Any(s=>s.Active)) || extractionQueue?.Jobs.Any(j=>j.State==ExtractionState.Running)==true)
                throw new InvalidOperationException("Close running games and finish library operations before installing an update.");
            await AppUpdates.VerifyAsync(path,release,default);
            if (operation is not null || library.Games.Any(g=>g.Sessions.Any(s=>s.Active)) || extractionQueue?.Jobs.Any(j=>j.State==ExtractionState.Running)==true) throw new InvalidOperationException("Finish active operations before updating.");
            Commit(); store.Save(library); preferenceStore.Save(preferences);
            void StartInstaller(object? sender,EventArgs e)
            {
                try { Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"msiexec.exe")) { UseShellExecute=false, ArgumentList={"/i",path,"/norestart","/l*v",path+".install.log"} }); }
                catch { MessageBox.Show("Windows Installer could not start. The verified installer is retained at: " + path + ". You can retry it or choose another download location.","ArcHive Update"); }
            }
            Closed+=StartInstaller; Close(); if (IsVisible) Closed-=StartInstaller;
        }
        catch (Exception ex) { MessageBox.Show(this,ex.Message,"Update not installed"); }
    }
}
