using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArcHive.Core;
using Microsoft.Win32;
namespace ArcHive.App;

internal static class IntegrationSession
{
    public static string Data => Environment.GetEnvironmentVariable("ARCHIVE_DATA_DIRECTORY") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArcHive");
    public static PluginCatalog Catalog => new(Data);
    public static IEnumerable<ILaunchAdapter> Adapters(AppPreferences preferences)
    {
        if (!string.IsNullOrWhiteSpace(preferences.LocaleEmulatorPath)) yield return new LocaleEmulatorAdapter(preferences.LocaleEmulatorPath);
        foreach (var plugin in Catalog.List().Where(p => p.Enabled && p.Manifest.Capabilities.Contains("launch"))) yield return new PluginLaunchAdapter(Catalog, plugin.Manifest.Id);
    }
}
public sealed record AdapterChoice(string? Id, string Name) { public override string ToString() => Name; }

public sealed class IntegrationWindow : Window
{
    private readonly TextBox locale = new();
    private readonly ListBox profiles = new() { MaxHeight = 130 };
    private readonly ListBox plugins = new() { MinHeight = 90, MaxHeight = 150 };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 10) };
    private readonly AppPreferences preferences;
    public IntegrationWindow(AppPreferences preferences)
    {
        this.preferences = preferences; Title = "Integrations & Plugins"; Width = 850; Height = 790; MinHeight = 650; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(8, 13, 11)); Foreground = Brushes.White;
        var stack = new StackPanel { Margin = new Thickness(24) }; Content = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        void Text(string text, double size = 13) => stack.Children.Add(new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 10) });
        Button Button(string title, Action action) { var button = new Button { Content = title, HorizontalAlignment = HorizontalAlignment.Left }; button.Click += (_, _) => Run(action); return button; }
        Text("Locale Emulator", 22); Text("Choose your existing LEProc.exe. ArcHive does not install or bundle Locale Emulator. Select its locale/time-zone profile separately for each game's Launch Profile; the code page comes from that locale. Only 32-bit games are supported.");
        locale.Text = preferences.LocaleEmulatorPath; stack.Children.Add(locale);
        var actions = new WrapPanel(); stack.Children.Add(actions);
        actions.Children.Add(Button("Choose LEProc.exe…", () => { var picker = new OpenFileDialog { Filter = "Locale Emulator|LEProc.exe" }; if (picker.ShowDialog(this) == true) { locale.Text = picker.FileName; ReadProfiles(); } }));
        actions.Children.Add(Button("Detect Installation", () =>
        {
            var roots = new[] { Path.GetDirectoryName(locale.Text) ?? "", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Locale Emulator"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Locale Emulator") }.Concat((Environment.GetEnvironmentVariable("PATH") ?? "").Split(';')).Where(Directory.Exists);
            var found = LocaleEmulatorAdapter.Detect(roots); if (found.Count > 0) { locale.Text = found[0]; ReadProfiles(); } else status.Text = "No complete installation found. Choose LEProc.exe manually.";
        }));
        actions.Children.Add(Button("Read Profiles", ReadProfiles)); actions.Children.Add(Button("Clear Selection", () => { locale.Clear(); profiles.ItemsSource = null; }));
        stack.Children.Add(profiles); Text("Locale Emulator owns these profiles. Edit locales and time zones in its profile manager, then read them again. Profiles may request elevation or start the emulator's updater. Custom working directories are not supported.");
        Text("Plugins", 22); Text("Install a plugin distribution folder containing plugin.json. Plugins run as your Windows user in separate processes; they are not sandboxed and can access files or the network. Enable only plugins you trust. ArcHive does not pass provider credentials to plugins.");
        stack.Children.Add(plugins); var pluginActions = new WrapPanel(); stack.Children.Add(pluginActions);
        pluginActions.Children.Add(Button("Install Plugin…", () => { var picker = new OpenFolderDialog { Title = "Choose Plugin Folder" }; if (picker.ShowDialog(this) == true) { IntegrationSession.Catalog.Install(picker.FolderName); Refresh(); status.Text = "Plugin installed and disabled. Review its capabilities before enabling it."; } }));
        pluginActions.Children.Add(Button("Enable Plugin…", () => { var p = Selected(); if (ChoiceDialog.Confirm(this, "Enable Plugin", $"Enable {p.Manifest.Name}?\nCapabilities: {string.Join(", ", p.Manifest.Capabilities)}\n\nThis plugin runs with your Windows account's access to files and the network. Only enable it if you trust its publisher.", "Enable Plugin")) { IntegrationSession.Catalog.SetEnabled(p.Manifest.Id, true); Refresh(); } }));
        pluginActions.Children.Add(Button("Disable Plugin", () => { IntegrationSession.Catalog.SetEnabled(Selected().Manifest.Id, false); Refresh(); }));
        pluginActions.Children.Add(Button("Remove Plugin…", () => { var p = Selected(); if (ChoiceDialog.Confirm(this, "Remove Plugin", "Remove the managed copy of this plugin? Its original distribution and game files will remain. Launch profiles that use it will need another adapter.", "Remove Plugin")) { IntegrationSession.Catalog.Remove(p.Manifest.Id); Refresh(); } }));
        stack.Children.Add(status); Text("Plugin changes take effect immediately. Locale Emulator settings are saved with your preferences.");
        stack.Children.Add(Button("Save Integration Settings", () => { if (!string.IsNullOrWhiteSpace(locale.Text)) LocaleEmulatorAdapter.ReadProfiles(locale.Text.Trim()); preferences.LocaleEmulatorPath = locale.Text.Trim(); DialogResult = true; }));
        stack.Children.Add(Button("Cancel", () => DialogResult = false));
        Run(Refresh);
    }
    private InstalledPlugin Selected() => plugins.SelectedItem as InstalledPlugin ?? throw new InvalidOperationException("Select a plugin first.");
    private void Refresh() => plugins.ItemsSource = IntegrationSession.Catalog.List();
    private void ReadProfiles() { profiles.ItemsSource = LocaleEmulatorAdapter.ReadProfiles(locale.Text.Trim()); status.Text = "Profiles loaded. Choose a profile in the game's Launch tab."; }
    private void Run(Action action) { try { action(); } catch (Exception ex) { status.Text = ex.Message; } }
}
