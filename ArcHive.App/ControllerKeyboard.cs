using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ArcHive.Core;

namespace ArcHive.App;

internal sealed class ControllerKeyboard : Window
{
    private readonly TextBlock preview = new() { Margin = new Thickness(8), FontSize = 20, TextWrapping = TextWrapping.Wrap, MaxHeight = 90 };
    private readonly UniformGrid keys = new() { Columns = 10 };
    private readonly bool secret;
    private bool upper;
    public string Value { get; private set; }
    public ControllerKeyboard(string value, bool secret)
    {
        this.secret = secret; Value = value;
        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(8, 13, 11));
        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(225, 240, 230));
        if (Application.Current?.TryFindResource("ArcHiveIcon") is System.Windows.Media.ImageSource icon) Icon = icon;
        Title = "Enter text"; Width = 800; Height = 530; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize;
        var root = new DockPanel { Margin = new Thickness(22) };
        var help = new TextBlock { Text = "D-pad: move   Cross / A: select   Circle / B: discard   Options / Start: menu", Margin = new Thickness(8) };
        DockPanel.SetDock(help, Dock.Bottom); root.Children.Add(help);
        DockPanel.SetDock(preview, Dock.Top); root.Children.Add(preview); root.Children.Add(keys); Content = root;
        RenderKeys(); Loaded += (_, _) => ((Button)keys.Children[0]).Focus();
        Closed += (_, _) => { preview.Text = ""; if (DialogResult != true) Value = ""; };
    }
    private void RenderKeys()
    {
        keys.Children.Clear();
        foreach (var c in "1234567890qwertyuiopasdfghjkl;zxcvbnm,./-_=+[]{}\\:'\"!?@#$%^&*()<>`~|")
        {
            var text = upper ? char.ToUpperInvariant(c).ToString() : c.ToString();
            Add(text, () => { if (Value.Length < 4096) Value += text; UpdatePreview(); });
        }
        Add("Space", () => { if (Value.Length < 4096) Value += " "; UpdatePreview(); });
        Add("Backspace", () => { if (Value.Length > 0) Value = Value[..^1]; UpdatePreview(); });
        Add("Clear", () => { Value = ""; UpdatePreview(); });
        Add(upper ? "Lowercase" : "Uppercase", () => { var index = keys.Children.IndexOf((UIElement)Keyboard.FocusedElement); upper = !upper; RenderKeys(); ((Button)keys.Children[index]).Focus(); });
        Add("New line", () => { if (Value.Length < 4096) Value += "\n"; UpdatePreview(); });
        Add("Use text", () => DialogResult = true); Add("Cancel", () => DialogResult = false);
        UpdatePreview();
    }
    private void Add(string text, Action action)
    {
        var button = new Button { Content = text, Padding = new Thickness(4), Margin = new Thickness(3), MinHeight = 36 };
        button.Click += (_, _) => action(); keys.Children.Add(button);
    }
    private void UpdatePreview() => preview.Text = secret ? new string('•', Value.Length) : Value.Length == 0 ? "Enter text…" : Value;
    public void Handle(PadAction action)
    {
        if (action == PadAction.Cancel) { DialogResult = false; return; }
        var index = keys.Children.IndexOf(Keyboard.FocusedElement as UIElement);
        if (action == PadAction.Accept && index >= 0) { ((Button)keys.Children[index]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); return; }
        if (action == PadAction.Next) action = PadAction.Right;
        if (action == PadAction.Previous) action = PadAction.Left;
        var next = GamepadNavigation.Move(index, keys.Children.Count, 10, action);
        if (next >= 0) ((Button)keys.Children[next]).Focus();
    }
}
