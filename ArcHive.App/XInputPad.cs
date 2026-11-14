using System.Runtime.InteropServices;
using ArcHive.Core;

namespace ArcHive.App;

internal static class XInputPad
{
    public static (int Index, PadButtons Buttons, short X, short Y)? Read()
    {
        try
        {
            for (uint index = 0; index < 4; index++)
            {
                if (GetState(index, out var state) != 0) continue;
                var b = state.Gamepad.Buttons; var buttons = PadButtons.None;
                if ((b & 1) != 0) buttons |= PadButtons.Up; if ((b & 2) != 0) buttons |= PadButtons.Down;
                if ((b & 4) != 0) buttons |= PadButtons.Left; if ((b & 8) != 0) buttons |= PadButtons.Right;
                if ((b & 0x1000) != 0) buttons |= PadButtons.A; if ((b & 0x2000) != 0) buttons |= PadButtons.B;
                if ((b & 0x10) != 0) buttons |= PadButtons.Menu;
                if ((b & 0x4000) != 0) buttons |= PadButtons.Keyboard;
                if ((b & 0x8000) != 0) buttons |= PadButtons.Options;
                if ((b & 0x100) != 0) buttons |= PadButtons.Previous; if ((b & 0x200) != 0) buttons |= PadButtons.Next;
                return ((int)index, buttons, state.Gamepad.LeftX, state.Gamepad.LeftY);
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }
        return null;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Pad { public ushort Buttons; public byte LeftTrigger, RightTrigger; public short LeftX, LeftY, RightX, RightY; }
    [StructLayout(LayoutKind.Sequential)] private struct State { public uint Packet; public Pad Gamepad; }
    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")] private static extern uint GetState(uint index, out State state);
}
