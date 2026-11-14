namespace ArcHive.Core;

[Flags]
public enum PadButtons { None = 0, Up = 1, Down = 2, Left = 4, Right = 8, A = 16, B = 32, Previous = 64, Next = 128 }
public enum PadAction { None, Up, Down, Left, Right, Accept, Cancel, Previous, Next }
public sealed class GamepadNavigation
{
    private bool armed;
    private PadButtons previous;
    private PadAction held;
    private TimeSpan repeatAt;
    public PadAction Update(PadButtons buttons, short x, short y, bool enabled, TimeSpan now)
    {
        if (!enabled) { armed = false; previous = PadButtons.None; held = PadAction.None; return PadAction.None; }
        buttons = EffectiveButtons(buttons, x, y);
        if (!armed) { if (buttons == PadButtons.None) armed = true; previous = buttons; return PadAction.None; }
        var pressed = buttons & ~previous; previous = buttons;
        if ((pressed & PadButtons.B) != 0) return PadAction.Cancel;
        if ((pressed & PadButtons.A) != 0) return PadAction.Accept;
        if ((pressed & PadButtons.Previous) != 0) return PadAction.Previous;
        if ((pressed & PadButtons.Next) != 0) return PadAction.Next;
        var direction = (buttons & PadButtons.Up) != 0 ? PadAction.Up : (buttons & PadButtons.Down) != 0 ? PadAction.Down :
            (buttons & PadButtons.Left) != 0 ? PadAction.Left : (buttons & PadButtons.Right) != 0 ? PadAction.Right : PadAction.None;
        if (direction == PadAction.None) { held = direction; return PadAction.None; }
        if (direction != held) { held = direction; repeatAt = now + TimeSpan.FromMilliseconds(450); return direction; }
        if (now < repeatAt) return PadAction.None;
        repeatAt = now + TimeSpan.FromMilliseconds(120); return direction;
    }
    public static PadButtons EffectiveButtons(PadButtons buttons, short x, short y)
    {
        if (Math.Abs((int)x) > 12000 || Math.Abs((int)y) > 12000)
            buttons |= Math.Abs((int)x) > Math.Abs((int)y) ? x < 0 ? PadButtons.Left : PadButtons.Right : y < 0 ? PadButtons.Down : PadButtons.Up;
        return buttons;
    }
    public static int Move(int index, int count, int columns, PadAction action)
    {
        if (count <= 0) return -1;
        if (index < 0) return 0;
        columns = Math.Max(1, columns);
        return action switch
        {
            PadAction.Left => index % columns == 0 ? index : index - 1,
            PadAction.Right => index % columns == columns - 1 ? index : Math.Min(count - 1, index + 1),
            PadAction.Up => Math.Max(0, index - columns), PadAction.Down => Math.Min(count - 1, index + columns), _ => Math.Min(count - 1, index)
        };
    }
}
