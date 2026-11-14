using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace ArcHive.App;
public sealed class ErrorDialog : Window
{
    public static void ShowDetails(Window owner, string message, Exception error)
    {
        var dialog = new ErrorDialog(message, error); if (owner.IsVisible) dialog.Owner = owner; dialog.ShowDialog();
    }
    private ErrorDialog(string message, Exception error)
    {
        Title = "ArcHive — Could Not Complete"; Width = 680; SizeToContent = SizeToContent.Height; MaxHeight = 650; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(8,13,11)); Foreground = Brushes.White;
        var body = new StackPanel { Margin = new Thickness(24) }; Content = body;
        body.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,16) });
        body.Children.Add(new Expander { Header = "Technical Details", Content = new TextBox { Text = error.ToString(), IsReadOnly = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 300, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
        var close = new Button { Content = "Close", IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right }; body.Children.Add(close);
    }
}
