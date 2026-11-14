using System.Runtime.InteropServices;
using System.Windows.Input;

namespace ArcHive.App;

// Used only for ArcHive-owned native file/confirmation dialogs. Never controls other apps.
internal static class ControllerKeys
{
    public static IntPtr Foreground => GetForegroundWindow();
    public static bool Owns(IntPtr window) => window != IntPtr.Zero && GetWindowThreadProcessId(window, out var id) != 0 && id == Environment.ProcessId;
    public static bool Send(IntPtr target, Key key, bool shift = false, bool control = false)
    {
        if (target != Foreground || !Owns(target)) return false;
        var inputs = new List<Input>();
        void Add(Key k, bool up) => inputs.Add(new() { Type = 1, Data = new() { Keyboard = new() { VirtualKey = (ushort)KeyInterop.VirtualKeyFromKey(k), Flags = up ? 2u : 0 } } });
        if (shift) Add(Key.LeftShift, false); if (control) Add(Key.LeftCtrl, false);
        Add(key, false); Add(key, true);
        if (control) Add(Key.LeftCtrl, true); if (shift) Add(Key.LeftShift, true);
        return SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<Input>()) == inputs.Count;
    }
    public static void Text(IntPtr target, string text)
    {
        if (target != Foreground || !Owns(target)) return;
        var inputs = new List<Input>();
        foreach (var c in text)
        {
            inputs.Add(new() { Type = 1, Data = new() { Keyboard = new() { Scan = c, Flags = 4 } } });
            inputs.Add(new() { Type = 1, Data = new() { Keyboard = new() { Scan = c, Flags = 6 } } });
        }
        if (inputs.Count > 0) SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<Input>());
    }
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public Union Data; }
    [StructLayout(LayoutKind.Explicit)] private struct Union { [FieldOffset(0)] public KeyboardInput Keyboard; [FieldOffset(0)] public MouseInput Mouse; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { public ushort VirtualKey, Scan; public uint Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern uint SendInput(uint count, Input[] inputs, int size);
}
