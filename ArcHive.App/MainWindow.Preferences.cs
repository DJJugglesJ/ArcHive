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
        ActiveControllerProfile = preferences.ControllerProfile;
        var prompts = ControllerPrompts.For(preferences.ControllerProfile);
        ControllerNavigationHint.Text = preferences.GamepadEnabled ? $"{prompts.Accept}: open / select · {prompts.Menu}: options · {prompts.Previous}/{prompts.Next}: views" : "Gamepad navigation off";
        NsfwLabel.Text = preferences.ShowNsfw ? "18+" : "18";
        NsfwCircle.Visibility = NsfwSlash.Visibility = preferences.ShowNsfw ? Visibility.Collapsed : Visibility.Visible;
        var state = preferences.ShowNsfw ? "NSFW games shown — click to hide" : "NSFW games hidden — click to show";
        NsfwToggle.ToolTip = state; System.Windows.Automation.AutomationProperties.SetName(NsfwToggle, state);
    }
    private void OpenSettings(object sender, RoutedEventArgs e) => Guard(() =>
    {
        var window = new SettingsWindow(preferences) { Owner = this };
        if (window.ShowDialog() != true) return;
        preferenceStore.Save(window.Result); preferences = window.Result; ShowPreferences();
    });
    private void ToggleNsfw(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (operation is not null) return;
        Commit();
        var next = new AppPreferences { ControllerProfile = preferences.ControllerProfile, GamepadEnabled = preferences.GamepadEnabled, ShowNsfw = !preferences.ShowNsfw };
        preferenceStore.Save(next); preferences = next; ShowPreferences(); Refresh();
    });
}
