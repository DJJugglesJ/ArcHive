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
        // Opt-in diagnostics for repeatable routing tests without a physical controller.
        // Requires an explicit isolated data directory; normal launches ignore these keys.
        if (Environment.GetEnvironmentVariable("ARCHIVE_CONTROLLER_TEST_INPUT") == "1" && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ARCHIVE_DATA_DIRECTORY")))
            EventManager.RegisterClassHandler(typeof(UIElement), Keyboard.PreviewKeyDownEvent, new KeyEventHandler((sender, e) =>
            {
                var action = e.Key switch { Key.F1 => PadAction.Accept, Key.F2 => PadAction.Cancel, Key.F3 => PadAction.Options, Key.F4 => PadAction.Menu, Key.F5 => PadAction.Keyboard, Key.F6 => PadAction.Previous, Key.F7 => PadAction.Next, Key.F8 => PadAction.Up, Key.F9 => PadAction.Down, Key.F11 => PadAction.Left, Key.F12 => PadAction.Right, _ => PadAction.None };
                if (action != PadAction.None && !e.Handled) { e.Handled = true; var window = Window.GetWindow(e.OriginalSource as DependencyObject) ?? Application.Current.Windows.Cast<Window>().FirstOrDefault(w => w.IsActive) ?? this; RouteController(window, action); }
            }));
        var reducer = new GamepadNavigation(); var clock = Stopwatch.StartNew(); string? controller = null; IntPtr previousTarget = IntPtr.Zero;
        var router = new ControllerRouter(); NativePlayStationInput? sony = null;
        SourceInitialized += (_, _) =>
        {
            sony = new NativePlayStationInput(HwndSource.FromHwnd(new WindowInteropHelper(this).Handle), router, clock);
            if (!sony.Registered) ControllerNavigationHint.Text += " · Native Sony input unavailable";
        };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        timer.Tick += (_, _) =>
        {
            var xbox = XInputPad.Read();
            ControllerReading? fallback = xbox is { } x ? new("xinput:" + x.Index, new(x.Buttons, x.X, x.Y), true) : null;
            var pad = router.Read(fallback, clock.Elapsed);
            var target = ControllerKeys.Foreground;
            if (controller != pad?.Identity || previousTarget != target)
            { reducer.Update(0, 0, 0, false, clock.Elapsed); controller = pad?.Identity; previousTarget = target; }
            var enabled = preferences.GamepadEnabled && ControllerKeys.Owns(target) && pad is { Ready: true };
            if (!enabled) router.Suspend();
            var action = reducer.Update(pad?.State.Buttons ?? 0, pad?.State.X ?? 0, pad?.State.Y ?? 0, enabled, clock.Elapsed);
            if (action == PadAction.None) return;
            var window = Application.Current.Windows.Cast<Window>().FirstOrDefault(w => new WindowInteropHelper(w).Handle == target);
            if (window is null) HandleNativeDialog(target, action);
            else RouteController(window, action);
        };
        Loaded += (_, _) => timer.Start(); Closed += (_, _) => { timer.Stop(); sony?.Dispose(); };
    }
    private void RouteController(Window window, PadAction action)
    {
        if (window is ControllerKeyboard keyboard) { if (ControllerFocus.HandleMenu(action)) return; if (action is PadAction.Menu or PadAction.Options) ShowApplicationMenu(keyboard); else keyboard.Handle(action); return; }
        if (ControllerFocus.HandleMenu(action)) return;
        if (action == PadAction.Menu) { ShowApplicationMenu(window); return; }
        if (window is UpdateWindow updates && updates.HandleController(action)) return;
        if (window is MetadataWindow metadata && metadata.HandleController(action)) return;
        var focused = Keyboard.FocusedElement as DependencyObject;
        if (action == PadAction.Keyboard || action == PadAction.Accept && focused is TextBox { IsReadOnly: false } or PasswordBox)
        { OpenControllerKeyboard(window, focused); return; }
        if (action == PadAction.Options)
        {
            if (window == this && selected is not null) ShowOptions();
            else if (focused is FrameworkElement { ContextMenu: { } menu } element) { menu.PlacementTarget = element; menu.IsOpen = true; FocusMenu(menu); }
            else ShowApplicationMenu(window);
            return;
        }
        if (window == this && action == PadAction.Cancel && ControllerFocus.Ancestor<ComboBox>(focused)?.IsDropDownOpen != true)
        {
            if (detailsPage) BackToCollection(this, new()); else FocusSelectedEntry();
            return;
        }
        if (window == this && !detailsPage && action is not (PadAction.Previous or PadAction.Next) &&
            (Gallery.IsKeyboardFocusWithin || ArchiveList.IsKeyboardFocusWithin || focused is null))
        { HandleNavigation(action); return; }
        ControllerFocus.Handle(window, action);
    }
    private void OpenControllerKeyboard(Window owner, DependencyObject? focused)
    {
        if (focused is not (TextBox { IsReadOnly: false } or PasswordBox)) return;
        var value = focused is TextBox text ? text.Text : ((PasswordBox)focused).Password;
        var keyboard = new ControllerKeyboard(value, focused is PasswordBox) { Owner = owner };
        if (keyboard.ShowDialog() == true)
        { if (focused is TextBox box) { box.Text = keyboard.Value; box.CaretIndex = box.Text.Length; } else ((PasswordBox)focused).Password = keyboard.Value; }
        ((UIElement)focused).Focus();
    }
    private void HandleNativeDialog(IntPtr target, PadAction action)
    {
        if (action == PadAction.Keyboard)
        {
            var keyboard = new ControllerKeyboard("", false);
            new WindowInteropHelper(keyboard).Owner = target;
            if (keyboard.ShowDialog() == true)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
                { if (ControllerKeys.Send(target, Key.A, control: true)) ControllerKeys.Text(target, keyboard.Value); }));
            }
            return;
        }
        var key = action switch
        {
            PadAction.Accept => Key.Enter, PadAction.Cancel => Key.Escape,
            PadAction.Up => Key.Up, PadAction.Down => Key.Down, PadAction.Left => Key.Left, PadAction.Right => Key.Right,
            PadAction.Previous or PadAction.Next => Key.Tab, PadAction.Options => Key.Apps, PadAction.Menu => Key.F10, _ => Key.None
        };
        if (key != Key.None) ControllerKeys.Send(target, key, shift: action == PadAction.Previous);
    }
    private static void FocusMenu(ContextMenu menu) => menu.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => menu.Items.OfType<MenuItem>().FirstOrDefault(i => i.IsEnabled)?.Focus()));
    private void ShowApplicationMenu(Window owner)
    {
        var menu = new ContextMenu { PlacementTarget = owner, Placement = PlacementMode.Center };
        void Add(string label, Action action, bool enabled = true)
        { var item = new MenuItem { Header = label, IsEnabled = enabled }; item.Click += (_, _) => { menu.IsOpen = false; action(); }; menu.Items.Add(item); }
        Add("Return to screen", () => { });
        if (owner == this)
        {
            Add("Library", () => { if (detailsPage) BackToCollection(this, new()); Navigate(false, false); }, operation is null);
            Add("Favorites", () => { if (detailsPage) BackToCollection(this, new()); Navigate(false, true); }, operation is null);
            Add("Archives", () => { if (detailsPage) BackToCollection(this, new()); Navigate(true, false); }, operation is null);
            Add("Add Library Folder…", () => AddRoot(this, new()), operation is null);
            Add("Import Owned Games…", () => OpenOwnedLibrary(this, new()), operation is null);
            Add("Activity", () => OpenExtractionQueue(this, new()));
            Add("Organize Library", () => OrganizeLibrary(this, new()), operation is null);
            Add("Settings", () => OpenSettings(this, new()), operation is null);
        }
        else { if (owner is ControllerKeyboard keyboard) Add("Use entered text", () => keyboard.DialogResult = true); Add("Cancel / back", owner.Close); }
        Add("Controller help", () => MessageBox.Show(owner, UiText.ControllerHelp, "Controller controls"));
        menu.Opened += (_, _) => FocusMenu(menu); menu.IsOpen = true;
    }
}
