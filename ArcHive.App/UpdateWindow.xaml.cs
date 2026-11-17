using System.IO;
using System.Net.Http;
using System.Windows;
using ArcHive.Core;
namespace ArcHive.App;
public partial class UpdateWindow : Window
{
    private readonly HttpClient client = new(new HttpClientHandler { AllowAutoRedirect=false, UseCookies=false });
    private string downloadFolder = Path.Combine(Path.GetTempPath(), "ArcHive", "Updates");
    private AppRelease? release;
    private CancellationTokenSource? pending;
    private bool closed;
    public string? InstallerPath { get; private set; }
    public AppRelease? InstallerRelease => release;
    public UpdateWindow(string data)
    {
        InitializeComponent(); DownloadLocation.Text=DisplayPaths.Compact(downloadFolder); DownloadLocation.ToolTip=downloadFolder; CurrentVersion.Text="Installed build: " + AppUpdates.Current;
        Closed += (_,_) => { closed=true; pending?.Cancel(); client.Dispose(); };
        Loaded += (_,_)=>CheckButton.Focus();
    }
    internal bool HandleController(PadAction action)
    { if (action == PadAction.Cancel && pending is not null) { pending.Cancel(); return true; } return false; }
    private void ChooseDownloadFolder(object sender, RoutedEventArgs e)
    {
        if (pending is not null) return;
        var picker = new Microsoft.Win32.OpenFolderDialog { Title = "Choose update download folder", InitialDirectory = Directory.Exists(downloadFolder) ? downloadFolder : Path.GetTempPath() };
        if (picker.ShowDialog(this) != true) return;
        downloadFolder = picker.FolderName; InstallerPath = null;
        DownloadLocation.Text=DisplayPaths.Compact(downloadFolder); DownloadLocation.ToolTip=downloadFolder;
        InstallButton.IsEnabled=false; DownloadButton.IsEnabled=release is not null && release.Version>AppUpdates.Current;
        Status.Text="Download location changed. Download the update here before installing.";
        (DownloadButton.IsEnabled ? DownloadButton : CheckButton).Focus();
    }
    private void Stop(object sender,RoutedEventArgs e) => pending?.Cancel();
    private async Task Run(Func<CancellationToken,Task> work)
    {
        if (pending is not null) return;
        using var request = new CancellationTokenSource(); pending=request;
        LocationButton.IsEnabled=false; CheckButton.IsEnabled=DownloadButton.IsEnabled=InstallButton.IsEnabled=false;
        StopButton.Visibility=Visibility.Visible; StopButton.Focus();
        try { await work(request.Token); }
        catch (OperationCanceledException) { if (!closed) Status.Text="Update operation stopped. Nothing was installed."; }
        catch (Exception) { if (!closed) Status.Text="The update could not be checked or verified. Nothing was installed. Check your connection and try again."; }
        finally
        {
            pending=null;
            if (!closed) { StopButton.Visibility=Visibility.Collapsed; Progress.Visibility=Visibility.Collapsed; LocationButton.IsEnabled=true; CheckButton.IsEnabled=true; DownloadButton.IsEnabled=release is not null && release.Version>AppUpdates.Current && InstallerPath is null; InstallButton.IsEnabled=InstallerPath is not null; (InstallButton.IsEnabled ? InstallButton : DownloadButton.IsEnabled ? DownloadButton : CheckButton).Focus(); }
        }
    }
    private async void Check(object sender,RoutedEventArgs e) => await Run(async token=>
    {
        release=null; InstallerPath=null; Notes.Text=""; Status.Text="Checking GitHub releases...";
        var found=await new AppUpdates(client).CheckAsync(token); token.ThrowIfCancellationRequested(); if (closed) return;
        release=found; Notes.Text=found.Notes;
        Status.Text=found.Version>AppUpdates.Current ? $"Version {found.Version} is available. Download and review before installing." : $"No newer packaged release. Latest published: {found.Version}.";
    });
    private async void Download(object sender,RoutedEventArgs e) => await Run(async token=>
    {
        if (release is null) return; InstallerPath=null; Progress.Value=0; Progress.Visibility=Visibility.Visible; Status.Text="Downloading and verifying installer...";
        var path=await new AppUpdates(client).DownloadAsync(release,downloadFolder,new Progress<double>(v=> { if (!closed) Progress.Value=v; }),token);
        if (closed) return; InstallerPath=path; Status.Text="Verified download ready. Install closes ArcHive and opens Windows Installer. Reopen ArcHive from the Start menu when installation finishes.";
    });
    private async void Install(object sender,RoutedEventArgs e) => await Run(async token=>
    {
        if (release is null || InstallerPath is null) return;
        try { await AppUpdates.VerifyAsync(InstallerPath,release,token); }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        { InstallerPath=null; Status.Text="The downloaded installer is missing or has changed. Download a fresh copy before installing."; return; }
        if (!closed) DialogResult=true;
    });
}
