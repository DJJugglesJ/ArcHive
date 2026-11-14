using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace ArcHive.App;
public sealed class ChoiceDialog : Window
{
    public static bool Confirm(Window owner, string title, string message, string action) => new ChoiceDialog(title, message, action) { Owner = owner }.ShowDialog() == true;
    public ChoiceDialog(string title, string message, string action)
    {
        Title = title; Width = 580; SizeToContent = SizeToContent.Height; MaxHeight = 700; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(8, 13, 11)); Foreground = Brushes.White;
        var stack = new StackPanel { Margin = new Thickness(24) }; Content = stack;
        stack.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 18) });
        var buttons = new WrapPanel(); stack.Children.Add(buttons);
        var confirm = new Button { Content = action }; confirm.Click += (_, _) => DialogResult = true; buttons.Children.Add(confirm);
        var cancel = new Button { Content = "Cancel", IsCancel = true }; buttons.Children.Add(cancel); Loaded += (_, _) => cancel.Focus();
    }
}
