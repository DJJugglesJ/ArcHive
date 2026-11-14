using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using ArcHive.Core;

namespace ArcHive.App;

public partial class MainWindow
{
    private void StartGamepad()
    {
        var reducer = new GamepadNavigation(); var clock = Stopwatch.StartNew(); string? controller = null;
        var router = new ControllerRouter(); NativePlayStationInput? sony = null;
        SourceInitialized += (_, _) =>
        {
            var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            sony = new NativePlayStationInput(source, router, clock);
            if (!sony.Registered) ControllerNavigationHint.Text += " · Native Sony input unavailable";
        };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        timer.Tick += (_, _) =>
        {
            var xbox = XInputPad.Read();
            ControllerReading? fallback = xbox is { } x ? new("xinput:" + x.Index, new(x.Buttons, x.X, x.Y), true) : null;
            var pad = router.Read(fallback, clock.Elapsed);
            if (controller != pad?.Identity) { reducer.Update(0, 0, 0, false, clock.Elapsed); controller = pad?.Identity; }
            var editing = Keyboard.FocusedElement is TextBoxBase { IsReadOnly: false } or PasswordBox or ComboBox or ComboBoxItem;
            var enabled = preferences.GamepadEnabled && IsActive && IsEnabled && operation is null && (!editing || optionsMenu?.IsOpen == true) && pad is { Ready: true };
            if (!enabled) router.Suspend();
            var action = reducer.Update(pad?.State.Buttons ?? 0, pad?.State.X ?? 0, pad?.State.Y ?? 0, enabled, clock.Elapsed);
            HandleNavigation(action);
        };
        Loaded += (_, _) => timer.Start(); Closed += (_, _) => { timer.Stop(); sony?.Dispose(); };
    }
}
