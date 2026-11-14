using System.IO;
using System.Windows;
using ArcHive.Core;

namespace ArcHive.App;

public partial class MainWindow
{
    public static readonly DependencyProperty ActiveControllerProfileProperty = DependencyProperty.Register(nameof(ActiveControllerProfile), typeof(ControllerProfile), typeof(MainWindow), new PropertyMetadata(ControllerProfile.DualSense));
    public ControllerProfile ActiveControllerProfile { get => (ControllerProfile)GetValue(ActiveControllerProfileProperty); set => SetValue(ActiveControllerProfileProperty, value); }
    private AppPreferences preferences = new();
    private AppPreferencesStore preferenceStore = null!;
    private void LoadPreferences()
    {
        preferenceStore = new(Path.Combine(data, "settings.json"));
        try { preferences = preferenceStore.Load(); }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or InvalidDataException or UnauthorizedAccessException)
        { preferences = new(); /* Keep a damaged settings file intact until the user saves a change. */ }
        ShowPreferences();
    }
    private void ShowPreferences()
    {
        F95ThreadButton.Visibility = preferences.EnableF95Zone ? Visibility.Visible : Visibility.Collapsed;
        ActiveControllerProfile = preferences.ControllerProfile;
        var prompts = ControllerPrompts.For(preferences.ControllerProfile);
        ControllerNavigationHint.Text = preferences.GamepadEnabled ? UiText.ControllerHint(preferences.ControllerProfile) : "Gamepad navigation off";
        NsfwLabel.Text = preferences.ShowNsfw ? "18+" : "18";
        NsfwCircle.Visibility = NsfwSlash.Visibility = preferences.ShowNsfw ? Visibility.Collapsed : Visibility.Visible;
        var state = preferences.ShowNsfw ? "NSFW games shown — click to hide" : "NSFW games hidden — click to show";
        NsfwToggle.ToolTip = state; System.Windows.Automation.AutomationProperties.SetName(NsfwToggle, state);
    }
    private void OpenSettings(object sender, RoutedEventArgs e) => Guard(() =>
    {
        var window = new SettingsWindow(preferences, library.Roots, data) { Owner = this };
        if (window.ShowDialog() != true) return;
        preferenceStore.Save(window.Result); preferences = window.Result; ShowPreferences(); galleryColumns = Math.Max(1, (int)(Gallery.ActualWidth / (preferences.LibraryDensity == LibraryDensity.Compact ? 190 : 240))); Refresh();
        switch (window.RequestedAction) { case "purge": PurgeLibrary(); break; case "add": AddRoot(this, new()); break; case "scan": Rescan(this, new()); break; case "organize": OrganizeLibrary(this, new()); break; case "import": OpenOwnedLibrary(this, new()); break; case "queue": OpenExtractionQueue(this, new()); break; }
    });
    private void ToggleNsfw(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (operation is not null) return;
        Commit();
        var next = System.Text.Json.JsonSerializer.Deserialize<AppPreferences>(System.Text.Json.JsonSerializer.Serialize(preferences))!; next.ShowNsfw = !preferences.ShowNsfw;
        preferenceStore.Save(next); preferences = next; ShowPreferences(); Refresh();
    });
}
