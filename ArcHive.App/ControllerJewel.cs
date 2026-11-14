using System.Windows;
using System.Windows.Media;
using ArcHive.Core;

namespace ArcHive.App;

// Vector jewels remain sharp at any DPI; labels are exposed to assistive technology.
public sealed class ControllerJewel : FrameworkElement
{
    public static readonly DependencyProperty ProfileProperty = DependencyProperty.Register(nameof(Profile), typeof(ControllerProfile), typeof(ControllerJewel), new FrameworkPropertyMetadata(ControllerProfile.DualSense, FrameworkPropertyMetadataOptions.AffectsRender, Changed));
    public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(nameof(Position), typeof(int), typeof(ControllerJewel), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender, Changed));
    public ControllerProfile Profile { get => (ControllerProfile)GetValue(ProfileProperty); set => SetValue(ProfileProperty, value); }
    public int Position { get => (int)GetValue(PositionProperty); set => SetValue(PositionProperty, value); }
    public ControllerJewel() { Width = Height = 22; Loaded += (_, _) => Label(); }
    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e) => ((ControllerJewel)sender).Label();
    private void Label()
    {
        var p = ControllerPrompts.For(Profile); var label = new[] { p.Accept, p.Cancel, p.Alternate, p.Menu }[Math.Clamp(Position, 0, 3)];
        System.Windows.Automation.AutomationProperties.SetName(this, label + " button");
    }
    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        var index = Math.Clamp(Position, 0, 3);
        var colors = Profile == ControllerProfile.Xbox ? new[] { "#79BE6B", "#DC7979", "#76A9D8", "#D4C36D" } :
            Profile == ControllerProfile.DualShock4 ? new[] { "#86A9D9", "#DD8F99", "#D99CCB", "#89C4AC" } : new[] { "#B9C9C0", "#B9C9C0", "#B9C9C0", "#B9C9C0" };
        var brush = (Brush)new BrushConverter().ConvertFromString(colors[index])!;
        var pen = new Pen(brush, 1.4);
        context.PushTransform(new ScaleTransform(ActualWidth / 22, ActualHeight / 22));
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(15, 25, 19)), new Pen(new SolidColorBrush(Color.FromRgb(63, 79, 69)), 1), new Point(11, 11), 10, 10);
        if (Profile == ControllerProfile.Xbox)
        {
            var text = new FormattedText(new[] { "A", "B", "X", "Y" }[index], System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            context.DrawText(text, new Point(11 - text.Width / 2, 11 - text.Height / 2));
        }
        else switch (index)
        {
            case 0: context.DrawLine(pen, new(7, 7), new(15, 15)); context.DrawLine(pen, new(15, 7), new(7, 15)); break;
            case 1: context.DrawEllipse(null, pen, new(11, 11), 5, 5); break;
            case 2: context.DrawRectangle(null, pen, new Rect(6.5, 6.5, 9, 9)); break;
            case 3:
                var geometry = Geometry.Parse("M 11,5 L 17,16 L 5,16 Z"); context.DrawGeometry(null, pen, geometry); break;
        }
        context.Pop();
    }
}
