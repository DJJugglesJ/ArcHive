using System.Diagnostics;
using System.Runtime.InteropServices;
namespace ArcHive.Core;

public sealed class ProcessSessionMonitor
{
    private readonly Dictionary<int, DateTime> observed = [];
    public bool HadChildren { get; private set; }
    public ProcessSessionMonitor(Process process) { observed[process.Id] = process.StartTime.ToUniversalTime(); }
    public bool IsRunning()
    {
        // Snapshot descendants while their parent identity can still be verified. Never claim an unobserved handoff.
        var parents = Snapshot(); var alive = new Dictionary<int, DateTime>();
        foreach (var pair in observed)
            if (Started(pair.Key) is { } started && started == pair.Value) alive[pair.Key] = started;
        bool added;
        do
        {
            added = false;
            foreach (var (pid, parent) in parents)
            {
                if (alive.ContainsKey(pid) || !alive.TryGetValue(parent, out var parentStart)) continue;
                if (Started(pid) is not { } start || start < parentStart) continue;
                alive[pid] = start; HadChildren = true; added = true;
            }
        } while (added);
        observed.Clear(); foreach (var pair in alive) observed[pair.Key] = pair.Value;
        return alive.Count > 0;
    }
    private static DateTime? Started(int id)
    { try { using var p = Process.GetProcessById(id); return p.HasExited ? null : p.StartTime.ToUniversalTime(); } catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { return null; } }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct Entry
    { public uint Size, Usage, Id; public UIntPtr Heap; public uint Module, Threads, Parent; public int Priority; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Name; }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint id);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool Process32FirstW(IntPtr snapshot, ref Entry entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool Process32NextW(IntPtr snapshot, ref Entry entry);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    private static List<(int Id, int Parent)> Snapshot()
    {
        var handle = CreateToolhelp32Snapshot(2, 0); if (handle == new IntPtr(-1)) throw new System.ComponentModel.Win32Exception();
        try { var result = new List<(int, int)>(); var entry = new Entry { Size = (uint)Marshal.SizeOf<Entry>(), Name = "" };
            if (!Process32FirstW(handle, ref entry)) throw new System.ComponentModel.Win32Exception();
            do { result.Add(((int)entry.Id, (int)entry.Parent)); } while (Process32NextW(handle, ref entry)); return result;
        } finally { CloseHandle(handle); }
    }
}
