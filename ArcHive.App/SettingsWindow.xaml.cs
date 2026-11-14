using System.Windows;
using System.Windows.Controls;
using ArcHive.Core;

namespace ArcHive.App;

public partial class SettingsWindow : Window
{
    public AppPreferences Result { get; private set; }
    public SettingsWindow(AppPreferences settings)
    {
        Result = new() { ControllerProfile = settings.ControllerProfile, GamepadEnabled = settings.GamepadEnabled, ShowNsfw = settings.ShowNsfw };
        InitializeComponent(); Profiles.ItemsSource = new[] { "DualSense", "DualShock 4 (DS4)", "Xbox" };
        Profiles.SelectedIndex = (int)settings.ControllerProfile; Gamepad.IsChecked = settings.GamepadEnabled;
    }
    private void ProfileChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Accept is null || Profiles.SelectedIndex < 0) return;
        Accept.Profile = CancelJewel.Profile = Alternate.Profile = Menu.Profile = (ControllerProfile)Profiles.SelectedIndex;
    }
    private void ShowAbout(object sender, RoutedEventArgs e) => new AboutWindow { Owner = this }.ShowDialog();
    private void Save(object sender, RoutedEventArgs e)
    { Result.ControllerProfile = (ControllerProfile)Profiles.SelectedIndex; Result.GamepadEnabled = Gamepad.IsChecked == true; DialogResult = true; }
}
