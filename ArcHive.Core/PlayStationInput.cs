using System.Buffers.Binary;

namespace ArcHive.Core;

public enum PlayStationModel { DualShock4, DualSense }
public readonly record struct PadState(PadButtons Buttons, short X, short Y);
public readonly record struct ControllerReading(string Identity, PadState State, bool Ready);

// Sony wire layouts, independently expressed for ArcHive's navigation subset.
// Protocol references and supported report variants are documented in NATIVE-CONTROLLERS.md.
public static class PlayStationInput
{
    public static PlayStationModel? Identify(uint vendor, uint product) => vendor != 0x054c ? null : product switch
    {
        0x05c4 or 0x09cc => PlayStationModel.DualShock4,
        0x0ce6 => PlayStationModel.DualSense,
        _ => null
    };
    public static bool TryDecode(uint vendor, uint product, ReadOnlySpan<byte> report, out PadState state)
    {
        state = default; var model = Identify(vendor, product);
        if (model is null || report.Length == 0) return false;
        int axes, buttons;
        if (model == PlayStationModel.DualSense)
        {
            if (report[0] == 0x01 && report.Length is 10 or 78) { axes = 1; buttons = 5; } // Bluetooth basic mode, including Windows padding.
            else if (report[0] == 0x01 && report.Length == 64) { axes = 1; buttons = 8; }
            else if (report[0] == 0x31 && report.Length == 78 && ValidBluetoothCrc(report)) { axes = 2; buttons = 9; }
            else return false;
        }
        else
        {
            if (report[0] == 0x01 && report.Length is 10 or 64 or 78 or 128)
            {
                // Extended DS4 USB/dongle state signals a missing controller at byte 31.
                if (report.Length >= 64 && (report[31] & 4) != 0) return false;
                axes = 1; buttons = 5;
            }
            else if (report[0] is >= 0x11 and <= 0x19 && report.Length == 78 && (report[1] & 0x80) != 0 && ValidBluetoothCrc(report))
            { axes = 3; buttons = 7; }
            else return false;
        }
        var face = report[buttons]; var shoulders = report[buttons + 1];
        var hat = face & 15;
        if (hat > 8) return false;
        var held = hat switch
        {
            0 => PadButtons.Up, 1 => PadButtons.Up | PadButtons.Right, 2 => PadButtons.Right,
            3 => PadButtons.Right | PadButtons.Down, 4 => PadButtons.Down, 5 => PadButtons.Down | PadButtons.Left,
            6 => PadButtons.Left, 7 => PadButtons.Left | PadButtons.Up, _ => PadButtons.None
        };
        if ((face & 0x20) != 0) held |= PadButtons.A; // Cross / south
        if ((face & 0x40) != 0) held |= PadButtons.B; // Circle / east
        if ((shoulders & 1) != 0) held |= PadButtons.Previous;
        if ((shoulders & 2) != 0) held |= PadButtons.Next;
        state = new(held, Axis(report[axes]), Axis(report[axes + 1], invert: true)); return true;
    }
    public static short Axis(byte value, bool invert = false)
    {
        // Both 127 and 128 are neutral. Convert to XInput's signed, up-positive coordinate space.
        var normalized = value < 127 ? (value - 127) * 32768 / 127 : value > 128 ? (value - 128) * 32767 / 127 : 0;
        return (short)(invert ? Math.Clamp(-normalized, short.MinValue, short.MaxValue) : normalized);
    }
    public static bool ValidBluetoothCrc(ReadOnlySpan<byte> report)
    {
        if (report.Length != 78) return false;
        uint crc = 0xffffffff;
        void Update(byte b) { crc ^= b; for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0); }
        Update(0xa1); foreach (var b in report[..^4]) Update(b);
        return ~crc == BinaryPrimitives.ReadUInt32LittleEndian(report[^4..]);
    }
    public static bool TryDecodeBatch(uint vendor, uint product, ReadOnlySpan<byte> rawHid, out PadState[] states)
    {
        states = [];
        if (rawHid.Length < 8 || Identify(vendor, product) is null) return false;
        var size = BinaryPrimitives.ReadUInt32LittleEndian(rawHid); var count = BinaryPrimitives.ReadUInt32LittleEndian(rawHid[4..]);
        if (size is < 10 or > 128 || count is 0 or > 128 || (ulong)size * count != (ulong)(rawHid.Length - 8)) return false;
        var parsed = new PadState[count];
        for (var i = 0; i < count; i++)
            if (!TryDecode(vendor, product, rawHid.Slice(8 + i * (int)size, (int)size), out parsed[i])) return false;
        states = parsed; return true;
    }
}

// UI-thread state router. Native Sony owns input while connected, suppressing XInput mirrors.
// State transitions are queued so a short press/release between timer ticks is not lost.
public sealed class ControllerRouter
{
    private sealed class Device(long sequence)
    {
        public long Sequence { get; } = sequence;
        public Queue<PadState> Pending { get; } = [];
        public PadState Current, Last;
        public bool HasLast, Ready;
        public TimeSpan Updated;
    }
    private readonly Dictionary<string, Device> native = [];
    private long sequence;
    private string? ownerId;
    public void Connect(string id)
    {
        if (!native.ContainsKey(id) && native.Count < 16) native.Add(id, new(++sequence));
    }
    public void Remove(string id)
    {
        native.Remove(id);
        if (ownerId == id) { ownerId = null; Suspend(); }
    }
    public void Reset(string id)
    {
        if (!native.TryGetValue(id, out var device)) return;
        device.Pending.Clear(); device.Ready = device.HasLast = false; device.Current = default;
    }
    public void Suspend() { foreach (var id in native.Keys) Reset(id); }
    public void Receive(string id, PadState state, TimeSpan now)
    {
        Connect(id); if (!native.TryGetValue(id, out var device)) return;
        device.Updated = now;
        if (!device.Ready)
        {
            // Real neutral input, not a synthetic timeout state, is needed before a new session.
            if (GamepadNavigation.EffectiveButtons(state.Buttons, state.X, state.Y) != 0) return;
            device.Ready = true;
        }
        if (!device.HasLast || GamepadNavigation.EffectiveButtons(device.Last.Buttons, device.Last.X, device.Last.Y) != GamepadNavigation.EffectiveButtons(state.Buttons, state.X, state.Y))
        {
            if (device.Pending.Count >= 32) { Reset(id); return; }
            device.Pending.Enqueue(state);
        }
        device.Last = state; device.HasLast = true;
    }
    public ControllerReading? Read(ControllerReading? xinput, TimeSpan now)
    {
        if (native.Count == 0) return xinput;
        var owner = native.OrderBy(pair => pair.Value.Sequence).First(); ownerId = owner.Key; var device = owner.Value;
        if (now - device.Updated > TimeSpan.FromSeconds(2)) Reset(owner.Key);
        if (!device.Ready) return new(owner.Key, default, false);
        if (device.Pending.TryDequeue(out var next)) device.Current = next;
        return new(owner.Key, device.Current, true);
    }
}
