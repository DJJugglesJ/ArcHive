using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ArcHive.Core;

namespace ArcHive.App;

internal sealed class LaunchConfirmation : Window
{
    public LaunchConfirmation(LaunchPlan plan, bool gamepad)
    {
        Title = "Launch game"; Width = 650; Height = 360; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var content = new StackPanel { Margin = new Thickness(24) };
        content.Children.Add(new TextBlock { Text = "Run this program? Only launch games you trust.", FontSize = 20 });
        content.Children.Add(new TextBox { Text = plan.CommandLine + "\n\nWorking directory: " + plan.WorkingDirectory, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Height = 160, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        content.Children.Add(new TextBlock { Text = "Release buttons, then A to launch · B to cancel" });
        var buttons = new WrapPanel(); var run = new Button { Content = "Launch" }; var cancel = new Button { Content = "Cancel", IsCancel = true };
        run.Click += (_, _) => DialogResult = true; cancel.Click += (_, _) => DialogResult = false;
        buttons.Children.Add(run); buttons.Children.Add(cancel); content.Children.Add(buttons); Content = content;
        var reducer = new GamepadNavigation(); var clock = Stopwatch.StartNew(); int? controller = null;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        timer.Tick += (_, _) =>
        {
            var pad = XInputPad.Read();
            if (controller != pad?.Index) { reducer.Update(0, 0, 0, false, clock.Elapsed); controller = pad?.Index; }
            var action = reducer.Update(pad?.Buttons ?? 0, pad?.X ?? 0, pad?.Y ?? 0, gamepad && IsActive && pad is not null, clock.Elapsed);
            if (action == PadAction.Accept) DialogResult = true;
            if (action == PadAction.Cancel) DialogResult = false;
        };
        Loaded += (_, _) => { cancel.Focus(); if (gamepad) timer.Start(); };
        Closed += (_, _) => timer.Stop();
    }
}
