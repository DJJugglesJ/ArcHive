using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using ArcHive.Core;

namespace ArcHive.App;

internal sealed class NativePlayStationInput : IDisposable
{
    private readonly HwndSource source;
    private readonly ControllerRouter router;
    private readonly Stopwatch clock;
    private readonly Dictionary<IntPtr, DeviceInfo> devices = [];
    private bool disposed;
    public bool Registered { get; }
    public NativePlayStationInput(HwndSource source, ControllerRouter router, Stopwatch clock)
    {
        this.source = source; this.router = router; this.clock = clock;
        source.AddHook(Hook);
        // Foreground only. No keyboard/mouse registration, background capture or device writes.
        Registered = RegisterRawInputDevices(Registrations(0x2000, source.Handle), 2, (uint)Marshal.SizeOf<Registration>());
        if (Registered) Enumerate();
    }
    private static Registration[] Registrations(uint flags, IntPtr target) =>
        [new() { Page = 1, Usage = 4, Flags = flags, Target = target }, new() { Page = 1, Usage = 5, Flags = flags, Target = target }];
    private static string Identity(IntPtr device) => "sony:" + device.ToInt64().ToString("X");
    private void Enumerate()
    {
        uint count = 0; var stride = (uint)Marshal.SizeOf<DeviceListEntry>();
        if (GetRawInputDeviceList(IntPtr.Zero, ref count, stride) == uint.MaxValue || count is 0 or > 1024) return;
        var capacity = count;
        var memory = Marshal.AllocHGlobal(checked((int)(count * stride)));
        try
        {
            var found = GetRawInputDeviceList(memory, ref count, stride);
            if (found == uint.MaxValue || found > capacity) return;
            for (var i = 0; i < found; i++)
            {
                var entry = Marshal.PtrToStructure<DeviceListEntry>(IntPtr.Add(memory, i * (int)stride));
                if (entry.Type == 2) Identify(entry.Handle);
            }
        }
        finally { Marshal.FreeHGlobal(memory); }
    }
    private bool Identify(IntPtr handle)
    {
        if (devices.ContainsKey(handle)) return true;
        var info = new DeviceInfo { Size = (uint)Marshal.SizeOf<DeviceInfo>() }; var size = info.Size;
        if (GetRawInputDeviceInfo(handle, 0x2000000b, ref info, ref size) == uint.MaxValue || info.Type != 2 || info.Page != 1 || info.Usage is not (4 or 5) || PlayStationInput.Identify(info.Vendor, info.Product) is null) return false;
        if (devices.Count >= 16) return false;
        devices[handle] = info; router.Connect(Identity(handle)); return true;
    }
    private IntPtr Hook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (disposed || !Registered) return IntPtr.Zero;
        if (message == 0x00fe) // WM_INPUT_DEVICE_CHANGE
        {
            if (wParam.ToInt64() == 2) { devices.Remove(lParam); router.Remove(Identity(lParam)); }
            else if (wParam.ToInt64() == 1)
            { devices.Remove(lParam); router.Remove(Identity(lParam)); Identify(lParam); }
        }
        else if (message == 0x00ff) Read(lParam);
        // Keep handled false: WPF/DefWindowProc must perform foreground WM_INPUT cleanup.
        return IntPtr.Zero;
    }
    private void Read(IntPtr input)
    {
        uint length = 0; var headerSize = (uint)Marshal.SizeOf<Header>();
        if (GetRawInputData(input, 0x10000003, IntPtr.Zero, ref length, headerSize) == uint.MaxValue || length < headerSize + 8 || length > 65536) return;
        var memory = Marshal.AllocHGlobal((int)length);
        try
        {
            var allocated = length;
            var copied = GetRawInputData(input, 0x10000003, memory, ref length, headerSize);
            if (copied == uint.MaxValue || copied > allocated || copied < headerSize + 8) return;
            var header = Marshal.PtrToStructure<Header>(memory);
            if (header.Type != 2 || header.Size != copied || !Identify(header.Device)) return;
            var info = devices[header.Device]; var id = Identity(header.Device);
            var bytes = new byte[copied - headerSize];
            Marshal.Copy(IntPtr.Add(memory, (int)headerSize), bytes, 0, bytes.Length);
            if (!PlayStationInput.TryDecodeBatch(info.Vendor, info.Product, bytes, out var states)) { router.Reset(id); return; }
            foreach (var state in states) router.Receive(id, state, clock.Elapsed);
        }
        finally { Marshal.FreeHGlobal(memory); }
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        if (Registered) RegisterRawInputDevices(Registrations(1, IntPtr.Zero), 2, (uint)Marshal.SizeOf<Registration>());
        source.RemoveHook(Hook);
        foreach (var handle in devices.Keys) router.Remove(Identity(handle));
        devices.Clear();
    }
    [StructLayout(LayoutKind.Sequential)] private struct Registration { public ushort Page, Usage; public uint Flags; public IntPtr Target; }
    [StructLayout(LayoutKind.Sequential)] private struct DeviceListEntry { public IntPtr Handle; public uint Type; }
    [StructLayout(LayoutKind.Sequential)] private struct Header { public uint Type, Size; public IntPtr Device, WParam; }
    [StructLayout(LayoutKind.Explicit, Size = 32)] private struct DeviceInfo
    {
        [FieldOffset(0)] public uint Size; [FieldOffset(4)] public uint Type;
        [FieldOffset(8)] public uint Vendor; [FieldOffset(12)] public uint Product;
        [FieldOffset(20)] public ushort Page; [FieldOffset(22)] public ushort Usage;
    }
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterRawInputDevices([In] Registration[] devices, uint count, uint size);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint GetRawInputDeviceList(IntPtr list, ref uint count, uint size);
    [DllImport("user32.dll", EntryPoint = "GetRawInputDeviceInfoW", SetLastError = true)] private static extern uint GetRawInputDeviceInfo(IntPtr device, uint command, ref DeviceInfo info, ref uint size);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint GetRawInputData(IntPtr input, uint command, IntPtr data, ref uint size, uint headerSize);
}
