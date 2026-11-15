using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using ArcHive.Core;
namespace ArcHive.App;
public partial class F95ThreadWindow : Window
{
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
    private static readonly F95ThreadSource Source = new(Client);
    private readonly Game game;
    private F95ThreadResult? result;
    private CancellationTokenSource? request;
    public F95ThreadWindow(Game game)
    {
        InitializeComponent(); this.game = game; ThreadUrl.Text = game.F95Thread?.Url ?? "";
        Evidence.Text = game.F95Thread is { } link ? $"Last observed: {link.CheckedAt:g}\n{link.ObservedTitle}\nVersion: {link.ObservedVersion}" : "Paste a thread link to begin.";
        Closing += (_, e) => { if (request is not null) { request.Cancel(); e.Cancel = true; Status.Text = "Canceling lookup; close again when finished."; } };
    }
    private void UrlChanged(object sender, TextChangedEventArgs e) { result = null; if (Review is not null) Review.IsEnabled = false; if (UseVersion is not null) { UseVersion.IsChecked = false; UseVersion.IsEnabled = false; } }
    private async void CheckThread(object sender, RoutedEventArgs e)
    {
        if (request is not null) return; request = new(); Actions.IsEnabled = ThreadUrl.IsEnabled = false; Status.Text = "Checking the public page…";
        result = null; Review.IsEnabled = UseVersion.IsEnabled = false; UseVersion.IsChecked = false;
        try { result = await Source.CheckAsync(ThreadUrl.Text, request.Token); Evidence.Text = $"{result.Candidate.Title}\nObserved version: {(result.Version.Length > 0 ? result.Version : "Not identified")}\nSource: {result.Url}\nChecked: {result.Candidate.RetrievedAt:g}\n\n" + string.Join("\n", result.Candidate.Fields.Select(f => $"{f.Key}: {f.Value}")); Review.IsEnabled = true; UseVersion.IsEnabled = result.Version.Length > 0; Status.Text = "Review the evidence before applying anything."; }
        catch (Exception ex) { Status.Text = ex is OperationCanceledException ? "Lookup canceled or timed out." : ex.Message; }
        finally { request.Dispose(); request = null; Actions.IsEnabled = ThreadUrl.IsEnabled = true; }
    }
    private void OpenThread(object sender, RoutedEventArgs e) { try { Process.Start(new ProcessStartInfo(F95ThreadSource.CanonicalUrl(ThreadUrl.Text)) { UseShellExecute = true }); } catch (Exception ex) { Status.Text = ex is ProviderException ? ex.Message : "Could not open the thread."; } }
    private void ReviewMetadata(object sender, RoutedEventArgs e) { if (result is not null) new MetadataWindow(game, result.Candidate) { Owner = this }.ShowDialog(); }
    private void Save(object sender, RoutedEventArgs e)
    {
        if (request is not null) return;
        try
        {
            var url = F95ThreadSource.CanonicalUrl(ThreadUrl.Text);
            var previous = game.F95Thread?.Url == url ? game.F95Thread : null;
            game.F95Thread = new() { Url = url, CheckedAt = result?.Candidate.RetrievedAt ?? previous?.CheckedAt, ObservedTitle = result?.Candidate.Title ?? previous?.ObservedTitle ?? "", ObservedVersion = result?.Version ?? previous?.ObservedVersion ?? "" };
            if (result is not null && UseVersion.IsChecked == true) { game.LatestRelease = result.Candidate.ObservedRelease is { } observed ? observed with { } : game.LatestRelease with { Version = result.Version }; game.LatestReleaseSource = url; game.LatestReleaseCheckedAt = result.Candidate.RetrievedAt; VersionTracking.Record(game, game.LatestRelease, url); }
            DialogResult = true;
        }
        catch (Exception ex) { Status.Text = ex.Message; }
    }
}
