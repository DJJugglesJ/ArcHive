using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using ArcHive.Core;

namespace ArcHive.App;

public partial class MainWindow
{
    public sealed class ArgumentEntry { public string Value { get; set; } = ""; }
    private readonly ObservableCollection<ArgumentEntry> argumentRows = [];
    private void AddArgument(object sender, RoutedEventArgs e) => argumentRows.Add(new());
    private void RemoveArgument(object sender, RoutedEventArgs e)
    { if (((Button)sender).Tag is ArgumentEntry row) argumentRows.Remove(row); }
    private bool loadingEditors;
    private GameArchive? editorArchive;
    private GameInstallation? editorInstallation;
    private LaunchProfile? editorProfile;

    private void CommitMilestone()
    {
        if (loadingEditors || selected is null) return;
        if (ProgressBox.SelectedItem is GameProgress progress) selected.Progress = progress;
        var latest = selected.LatestRelease with { Version = LatestVersionBox.Text.Trim(), Platform = LatestPlatformBox.Text.Trim(), Channel = LatestChannelBox.Text.Trim() };
        var source = string.IsNullOrWhiteSpace(LatestSourceBox.Text) ? "Manual" : LatestSourceBox.Text.Trim();
        if (latest != selected.LatestRelease || source != selected.LatestReleaseSource) selected.LatestReleaseCheckedAt = DateTimeOffset.UtcNow;
        selected.LatestRelease = latest; selected.LatestReleaseSource = source;
        VersionTracking.Record(selected, latest, source);
        CommitLocation();
    }
    private void LoadGameMilestone()
    {
        if (selected is null) return;
        ProgressBox.SelectedItem = selected.Progress;
        LatestVersionBox.Text = selected.LatestRelease.Version;
        LatestPlatformBox.Text = selected.LatestRelease.Platform;
        LatestChannelBox.Text = selected.LatestRelease.Channel;
        LatestSourceBox.Text = selected.LatestReleaseSource;
        ShowVersionSummary();
    }
    private void ShowVersionSummary()
    {
        if (selected is null) return;
        UpdateHint.Text = "Update Status: " + selected.UpdateStatus + (selected.LatestReleaseCheckedAt is { } checkedAt ? $" · Last Recorded {checkedAt.LocalDateTime:g}" : " · No known release recorded");
        VersionHistory.ItemsSource = selected.ReleaseHistory.OrderByDescending(h => h.ObservedAt)
            .Select(h => $"{h.Release.Version} · {h.Release.Platform} · {h.Release.Channel} · {h.Source} · {h.ObservedAt.LocalDateTime:g}").ToArray();
    }
    private void ClearMilestone()
    {
        loadingEditors = true;
        ProgressBox.SelectedItem = null;
        LatestVersionBox.Clear(); LatestPlatformBox.Clear(); LatestChannelBox.Clear(); LatestSourceBox.Clear();
        UpdateHint.Text = ""; VersionHistory.ItemsSource = null;
        loadingEditors = false; LoadLocationEditors();
    }
    private void CommitLocation()
    {
        if (loadingEditors || selected is null) return;
        if (editorArchive is { } archive)
        {
            archive.Release = archive.Release with { Version = ArchiveVersionBox.Text.Trim(), Platform = ArchivePlatformBox.Text.Trim(), Channel = ArchiveChannelBox.Text.Trim() };
            VersionTracking.Record(selected, archive.Release, "Archive " + archive.Id);
        }
        if (editorInstallation is { } installation)
        {
            installation.Release = installation.Release with { Version = InstalledVersionBox.Text.Trim(), Platform = InstalledPlatformBox.Text.Trim(), Channel = InstalledChannelBox.Text.Trim() };
            VersionTracking.Record(selected, installation.Release, "Installation " + installation.Id);
        }
        CommitProfile();
    }
    private void CommitProfile()
    {
        if (loadingEditors || editorProfile is null) return;
        editorProfile.Name = string.IsNullOrWhiteSpace(ProfileNameBox.Text) ? "Profile" : ProfileNameBox.Text.Trim();
        editorProfile.ExecutablePath = ProfileExecutableBox.Text.Trim();
        editorProfile.WorkingDirectory = ProfileWorkingBox.Text.Trim();
        editorProfile.Arguments = argumentRows.Select(row => row.Value).ToList();
        if (AdapterChoices.SelectedItem is AdapterChoice adapter) editorProfile.AdapterId = adapter.Id;
    }
    private void LoadLocationEditors()
    {
        loadingEditors = true;
        editorArchive = ActiveArchive; editorInstallation = ActiveInstallation;
        ArchiveVersionBox.Text = editorArchive?.Release.Version ?? "";
        ArchivePlatformBox.Text = editorArchive?.Release.Platform ?? "";
        ArchiveChannelBox.Text = editorArchive?.Release.Channel ?? "";
        ArchiveVersionBox.IsEnabled = ArchivePlatformBox.IsEnabled = ArchiveChannelBox.IsEnabled = editorArchive is not null;
        InstalledVersionBox.Text = editorInstallation?.Release.Version ?? "";
        InstalledPlatformBox.Text = editorInstallation?.Release.Platform ?? "";
        InstalledChannelBox.Text = editorInstallation?.Release.Channel ?? "";
        InstalledVersionBox.IsEnabled = InstalledPlatformBox.IsEnabled = InstalledChannelBox.IsEnabled = editorInstallation is not null;
        var previous = editorProfile?.Id;
        ProfileChoices.ItemsSource = editorInstallation?.Profiles.ToList();
        editorProfile = editorInstallation?.Profiles.FirstOrDefault(p => p.Id == previous)
            ?? editorInstallation?.Profiles.FirstOrDefault(p => p.Id == editorInstallation.DefaultProfileId)
            ?? editorInstallation?.Profiles.FirstOrDefault();
        ProfileChoices.SelectedItem = editorProfile;
        LoadProfileEditor(); loadingEditors = false; ShowVersionSummary();
    }
    private void LoadProfileEditor()
    {
        var choices = new List<AdapterChoice> { new(null, "Native Windows") };
        AdapterHint.Text = "Configure Locale Emulator and trusted plugins in Settings → Integrations.";
        try
        {
            if (!string.IsNullOrWhiteSpace(preferences.LocaleEmulatorPath))
                choices.AddRange(LocaleEmulatorAdapter.ReadProfiles(preferences.LocaleEmulatorPath).Select(p => new AdapterChoice(p.AdapterId, "Locale Emulator · " + p)));
            choices.AddRange(IntegrationSession.Catalog.List().Where(p => p.Enabled && p.Manifest.Capabilities.Contains("launch")).Select(p => new AdapterChoice("plugin:" + p.Manifest.Id, p.Manifest.Name)));
        }
        catch (Exception ex) { AdapterHint.Text = ex.Message; }
        if (editorProfile?.AdapterId is { Length: > 0 } missing && choices.All(c => c.Id != missing)) choices.Add(new(missing, "Unavailable: " + missing));
        AdapterChoices.ItemsSource = choices; AdapterChoices.SelectedItem = choices.FirstOrDefault(c => c.Id == editorProfile?.AdapterId) ?? choices[0];
        ProfileEditor.IsEnabled = editorProfile is not null;
        ProfileNameBox.Text = editorProfile?.Name ?? "";
        ProfileExecutableBox.Text = editorProfile?.ExecutablePath ?? "";
        argumentRows.Clear();
        if (editorProfile is not null) foreach (var argument in editorProfile.Arguments) argumentRows.Add(new() { Value = argument });
        ProfileWorkingBox.Text = editorProfile?.WorkingDirectory ?? "";
        DefaultProfileHint.Text = "Default: " + (editorInstallation?.Profiles.FirstOrDefault(p => p.Id == editorInstallation.DefaultProfileId)?.Name ?? "none");
    }
    private void ProfileChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loadingEditors) return;
        CommitProfile(); editorProfile = ProfileChoices.SelectedItem as LaunchProfile;
        LoadProfileEditor(); UpdateLocationSummary();
    }
    private void AddProfileModel()
    {
        if (editorInstallation is null) throw new InvalidOperationException("Select an installation first.");
        editorProfile = new LaunchProfile { Name = "Profile " + (editorInstallation.Profiles.Count + 1) };
        editorInstallation.Profiles.Add(editorProfile);
        editorInstallation.DefaultProfileId ??= editorProfile.Id;
    }
    private void NewProfile(object sender, RoutedEventArgs e) => Guard(() =>
    { Commit(); AddProfileModel(); LoadLocationEditors(); store.Save(library); });
    private void RemoveProfile(object sender, RoutedEventArgs e) => Guard(() =>
    {
        Commit();
        if (editorInstallation is null || editorProfile is null) return;
        editorInstallation.Profiles.Remove(editorProfile);
        if (editorInstallation.DefaultProfileId == editorProfile.Id) editorInstallation.DefaultProfileId = editorInstallation.Profiles.FirstOrDefault()?.Id;
        editorProfile = null; LoadLocationEditors(); store.Save(library);
    });
    private void DefaultProfile(object sender, RoutedEventArgs e) => Guard(() =>
    {
        Commit(); if (editorInstallation is null || editorProfile is null) return;
        editorInstallation.DefaultProfileId = editorProfile.Id; LoadProfileEditor(); store.Save(library);
    });
}
