using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcHive.App;
internal sealed class ControllerFocusRing(UIElement element) : Adorner(element)
{
    private static AdornerLayer? layer;
    private static ControllerFocusRing? ring;
    public static void Install()
    {
        EventManager.RegisterClassHandler(typeof(UIElement), Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler((_, e) =>
        {
            if (ring is not null) layer?.Remove(ring);
            ring = null;
            if (e.NewFocus is not FrameworkElement target || !target.IsVisible) return;
            target.BringIntoView(); layer = AdornerLayer.GetAdornerLayer(target);
            if (layer is null) return;
            ring = new(target) { IsHitTestVisible = false }; layer.Add(ring);
        }), true);
    }
    protected override void OnRender(DrawingContext drawingContext)
    {
        var rect = new Rect(AdornedElement.RenderSize); rect.Inflate(-1, -1);
        if (rect.Width > 0 && rect.Height > 0) drawingContext.DrawRoundedRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(179, 255, 208)), 2), rect, 4, 4);
    }
}
