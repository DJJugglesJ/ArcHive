using ArcHive.Core;
using ArcHive.App;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

internal static class ControllerWorkflowTests
{
    public static void Run(Action<bool, string> check)
    {
        foreach (var (product, length, buttons) in new[] { (0x05c4u, 64, 5), (0x09ccu, 64, 5), (0x0ce6u, 64, 8), (0x0ce6u, 10, 5) })
        {
            var report = new byte[length]; report[0] = 1; report[1] = report[2] = 128; report[buttons] = 0x98; report[buttons + 1] = 0x20;
            check(PlayStationInput.TryDecode(0x054c, product, report, out var state) && state.Buttons == (PadButtons.Options | PadButtons.Menu | PadButtons.Keyboard), "Sony distinguishes Triangle, Options and Square for " + product + "/" + length);
        }
        foreach (var (button, expected) in new[] { (PadButtons.A, PadAction.Accept), (PadButtons.B, PadAction.Cancel), (PadButtons.Options, PadAction.Options), (PadButtons.Menu, PadAction.Menu), (PadButtons.Keyboard, PadAction.Keyboard) })
        {
            var reducer = new GamepadNavigation();
            check(reducer.Update(button, 0, 0, true, TimeSpan.Zero) == PadAction.None, "Held input cannot activate a new dialog: " + expected);
            reducer.Update(0, 0, 0, true, TimeSpan.Zero);
            check(reducer.Update(button, 0, 0, true, TimeSpan.Zero) == expected && reducer.Update(button, 0, 0, true, TimeSpan.FromSeconds(2)) == PadAction.None, "Action is edge-triggered: " + expected);
            reducer.Update(0, 0, 0, false, TimeSpan.Zero);
            check(reducer.Update(button, 0, 0, true, TimeSpan.Zero) == PadAction.None, "Focus transitions require release: " + expected);
        }
        check(!ControllerKeys.Owns(IntPtr.Zero) && !ControllerKeys.Send(IntPtr.Zero, Key.Enter), "Native input refuses a missing or foreign target");
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var keyboard = new ControllerKeyboard("original", false);
                var root = (DockPanel)keyboard.Content;
                var keys = root.Children.OfType<System.Windows.Controls.Primitives.UniformGrid>().Single();
                void Click(string label) => keys.Children.OfType<Button>().Single(b => (string)b.Content == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Click("Clear"); Click("a"); Click("Space"); Click("1");
                check(keyboard.Value == "a 1", "Controller keyboard supports text, spaces and explicit clear");
                Click("Backspace"); check(keyboard.Value == "a ", "Controller keyboard removes one character");
                keyboard.Close();
                var secret = new ControllerKeyboard("test-secret", true);
                var preview = ((DockPanel)secret.Content).Children.OfType<TextBlock>().Last();
                check(preview.Text == new string('•', 11), "Credential keyboard masks text");
                secret.Close(); check(secret.Value == "", "Cancelled keyboard discards its edit buffer");
                var panel = new StackPanel(); var box = new ComboBox(); panel.Children.Add(box);
                var choice = new ComboBoxItem { Content = "Choice" }; box.Items.Add(choice);
                check(ControllerFocus.Ancestor<ComboBox>(choice) == box, "Popup choice resolves its owning selector for Circle/back");
                check(ControllerFocus.Ancestor<StackPanel>(box) == panel, "Controller finds owning controls through the visual tree");
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(15))) throw new TimeoutException("Controller workflow tests timed out.");
        if (failure is not null) throw new Exception("Controller workflow test failed", failure);
    }
}
