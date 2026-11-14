using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;
namespace ArcHive.App;
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        AboutIcon.Source = BitmapDecoder.Create(new Uri("pack://application:,,,/Assets/ArcHive.ico"), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames.MaxBy(frame => frame.PixelWidth);
        var build = typeof(AboutWindow).Assembly;
        // Keep prerelease labels, but omit the SDK's source-revision suffix.
        var version = build.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
            ?? build.GetName().Version?.ToString(3) ?? "Unknown";
        VersionLabel.Text = $"Version {version}";
        DeveloperLabel.Text = $"Developed by {build.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company}";
        CopyrightLabel.Text = build.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;
    }
}
