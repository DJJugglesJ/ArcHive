using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace ArcHive.App;

internal static class MetadataSourceLinks
{
    public static void Open(RequestNavigateEventArgs e)
    {
        e.Handled = true;
        if (e.Uri.Scheme != "https" || !new[] { "store.steampowered.com", "www.igdb.com", "itch.io", "rawg.io", "vndb.org" }.Contains(e.Uri.Host)) return;
        try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
        catch { MessageBox.Show("The source website could not be opened."); }
    }
}
