using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Documents;
using ArcHive.Core;

namespace ArcHive.App;

// One navigation path for every WPF window, including nested modal dialogs.
internal static class ControllerFocus
{
    public static T? Ancestor<T>(DependencyObject? item) where T : DependencyObject
    {
        while (item is not null)
        {
            if (item is T result) return result;
            if (item is ComboBoxItem choice && ItemsControl.ItemsControlFromItemContainer(choice) is ComboBox owner) item = owner;
            else item = item is Visual ? VisualTreeHelper.GetParent(item) : LogicalTreeHelper.GetParent(item);
        }
        return null;
    }
    public static void Move(Window window, bool previous)
    {
        if (Keyboard.FocusedElement is FrameworkContentElement content && Window.GetWindow(content) == window)
        { content.MoveFocus(new TraversalRequest(previous ? FocusNavigationDirection.Previous : FocusNavigationDirection.Next)); return; }
        var target = Keyboard.FocusedElement as UIElement;
        if (target is null || Window.GetWindow(target) != window) target = window;
        target.MoveFocus(new TraversalRequest(previous ? FocusNavigationDirection.Previous : FocusNavigationDirection.Next));
        (Keyboard.FocusedElement as FrameworkElement)?.BringIntoView();
    }
    public static bool HandleMenu(PadAction action)
    {
        if (Keyboard.FocusedElement is not MenuItem item) return false;
        if (ItemsControl.ItemsControlFromItemContainer(item) is not ContextMenu menu) return false;
        var items = menu.Items.OfType<MenuItem>().Where(i => i.IsEnabled && i.IsVisible).ToArray();
        if (action is PadAction.Cancel or PadAction.Options or PadAction.Menu) menu.IsOpen = false;
        else if (action == PadAction.Accept) { menu.IsOpen = false; item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); }
        else if (items.Length > 0 && action is PadAction.Up or PadAction.Down or PadAction.Previous or PadAction.Next)
        {
            var step = action is PadAction.Up or PadAction.Previous ? -1 : 1;
            items[(Array.IndexOf(items, item) + items.Length + step) % items.Length].Focus();
        }
        return true;
    }
    public static void Handle(Window window, PadAction action)
    {
        if (HandleMenu(action)) return;
        if (action is PadAction.Previous or PadAction.Next) { Move(window, action == PadAction.Previous); return; }
        var focused = Keyboard.FocusedElement as DependencyObject;
        if (focused is UIElement { IsEnabled: false } or UIElement { IsVisible: false }) { Move(window, false); return; }
        var combo = Ancestor<ComboBox>(focused);
        if (combo is not null && combo.IsDropDownOpen)
        {
            if (action is PadAction.Cancel or PadAction.Accept) { combo.IsDropDownOpen = false; combo.Focus(); }
            else if (action is PadAction.Up or PadAction.Down) combo.SelectedIndex = Math.Clamp(combo.SelectedIndex + (action == PadAction.Up ? -1 : 1), 0, Math.Max(0, combo.Items.Count - 1));
            return;
        }
        if (action == PadAction.Cancel) { window.Close(); return; }
        if (action == PadAction.Accept)
        {
            if (Ancestor<DataGrid>(focused) is { SelectionMode: DataGridSelectionMode.Extended })
            { ControllerKeys.Send(ControllerKeys.Foreground, Key.Space, control: true); return; }
            if (focused is ComboBox box) { box.IsDropDownOpen = !box.IsDropDownOpen; return; }
            if (focused is TabItem tab) { tab.IsSelected = true; return; }
            if (focused is Hyperlink hyperlink)
            { (new HyperlinkAutomationPeer(hyperlink).GetPattern(PatternInterface.Invoke) as IInvokeProvider)?.Invoke(); return; }
            if (focused is UIElement element)
            {
                var peer = UIElementAutomationPeer.CreatePeerForElement(element);
                if (peer?.GetPattern(PatternInterface.Toggle) is IToggleProvider toggle) { toggle.Toggle(); return; }
                if (peer?.GetPattern(PatternInterface.Invoke) is IInvokeProvider invoke) { invoke.Invoke(); return; }
                if (peer?.GetPattern(PatternInterface.ExpandCollapse) is IExpandCollapseProvider expand)
                { if (expand.ExpandCollapseState == System.Windows.Automation.ExpandCollapseState.Expanded) expand.Collapse(); else expand.Expand(); return; }
                if (peer?.GetPattern(PatternInterface.SelectionItem) is ISelectionItemProvider selection)
                {
                    if (Ancestor<ListBox>(element) is { SelectionMode: not SelectionMode.Single })
                    { if (selection.IsSelected) selection.RemoveFromSelection(); else selection.AddToSelection(); }
                    else selection.Select();
                    return;
                }
            }
            return;
        }
        if (action is PadAction.Up or PadAction.Down or PadAction.Left or PadAction.Right)
        {
            if (focused is ScrollViewer scroll)
            {
                if (action == PadAction.Up) scroll.LineUp(); else if (action == PadAction.Down) scroll.LineDown();
                else if (action == PadAction.Left) scroll.LineLeft(); else scroll.LineRight();
                return;
            }
            // Lists, tabs, sliders and grids keep their standard Windows arrow behavior.
            if (Ancestor<Selector>(focused) is not null || focused is Slider or TextBoxBase { IsReadOnly: true })
            {
                var key = action switch { PadAction.Up => Key.Up, PadAction.Down => Key.Down, PadAction.Left => Key.Left, _ => Key.Right };
                var multi = Ancestor<ListBox>(focused) is { SelectionMode: not SelectionMode.Single } || Ancestor<DataGrid>(focused) is { SelectionMode: DataGridSelectionMode.Extended };
                ControllerKeys.Send(ControllerKeys.Foreground, key, control: multi); return;
            }
            Move(window, action is PadAction.Up or PadAction.Left);
        }
    }
}
