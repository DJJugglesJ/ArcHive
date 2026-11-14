using System.Windows;
using System.Windows.Controls;
using ArcHive.Core;

namespace ArcHive.App;

public partial class SettingsWindow : Window
{
    private readonly string data;
    public string? RequestedAction { get; private set; }
    public AppPreferences Result { get; private set; }
    public SettingsWindow(AppPreferences settings, IEnumerable<string> roots, string data)
    {
        Result = System.Text.Json.JsonSerializer.Deserialize<AppPreferences>(System.Text.Json.JsonSerializer.Serialize(settings))!; this.data = data;
        InitializeComponent(); Profiles.ItemsSource = new[] { "DualSense", "DualShock 4 (DS4)", "Xbox" };
        Order.ItemsSource = new[] { "Title", "Recently played", "Favorites first" }; Order.SelectedIndex = (int)settings.LibraryOrder;
        Density.ItemsSource = Enum.GetValues<LibraryDensity>(); Density.SelectedItem = settings.LibraryDensity;
        F95Enabled.IsChecked = settings.EnableF95Zone; Roots.ItemsSource = roots.ToArray();
        DataLocation.Text = DisplayPaths.Compact(data); DataLocation.ToolTip = data;
        Profiles.SelectedIndex = (int)settings.ControllerProfile; Gamepad.IsChecked = settings.GamepadEnabled;
    }
    private void ProfileChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Accept is null || Profiles.SelectedIndex < 0) return;
        Accept.Profile = CancelJewel.Profile = Alternate.Profile = Menu.Profile = (ControllerProfile)Profiles.SelectedIndex;
    }
    private void Route(object sender, RoutedEventArgs e) { RequestedAction = ((Button)sender).Tag as string; Save(sender, e); }
    private void OpenData(object sender, RoutedEventArgs e) { try { System.IO.Directory.CreateDirectory(data); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(System.IO.Path.GetFullPath(data)) { UseShellExecute = true }); } catch { MessageBox.Show(this, "The data folder could not be opened."); } }
    private void ShowAbout(object sender, RoutedEventArgs e) => new AboutWindow { Owner = this }.ShowDialog();
    private void Save(object sender, RoutedEventArgs e)
    { Result.LibraryOrder = (LibraryOrder)Order.SelectedIndex; Result.LibraryDensity = (LibraryDensity)Density.SelectedItem; Result.EnableF95Zone = F95Enabled.IsChecked == true; Result.ControllerProfile = (ControllerProfile)Profiles.SelectedIndex; Result.GamepadEnabled = Gamepad.IsChecked == true; DialogResult = true; }
}
