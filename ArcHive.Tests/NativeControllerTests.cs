using System.Buffers.Binary;
using System.Diagnostics;
using System.Windows.Interop;
using ArcHive.App;
using ArcHive.Core;

internal static class NativeControllerTests
{
    public static void Run(Action<bool, string> check)
    {
        check(PlayStationInput.Identify(0x054c, 0x05c4) == PlayStationModel.DualShock4 && PlayStationInput.Identify(0x054c, 0x09cc) == PlayStationModel.DualShock4, "Identify original and second-generation DS4");
        check(PlayStationInput.Identify(0x054c, 0x0ce6) == PlayStationModel.DualSense, "Identify standard DualSense");
        check(PlayStationInput.Identify(0x045e, 0x0ce6) is null && PlayStationInput.Identify(0x054c, 0xffff) is null, "Unknown vendors and products cannot use Sony report offsets");
        // Literal fixtures specify distinct values in the different USB button layouts.
        var ds4 = new byte[64]; new byte[] { 1, 0, 255, 128, 128, 0x68, 3, 0, 0, 0 }.CopyTo(ds4, 0);
        check(PlayStationInput.TryDecode(0x054c, 0x05c4, ds4, out var state) && state.Buttons == (PadButtons.A | PadButtons.B | PadButtons.Previous | PadButtons.Next) && state.X == short.MinValue && state.Y == -32767, "DS4 USB maps Cross Circle shoulders and axes");
        var ds5 = new byte[64]; new byte[] { 1, 255, 0, 128, 128, 0xff, 0xff, 0x7f, 0x28, 2, 0 }.CopyTo(ds5, 0);
        check(PlayStationInput.TryDecode(0x054c, 0x0ce6, ds5, out state) && state.Buttons == (PadButtons.A | PadButtons.Next) && state.X == 32767 && state.Y == 32767, "DualSense USB distinguishes trigger and sequence bytes from buttons");
        foreach (var product in new uint[] { 0x05c4, 0x09cc, 0x0ce6 })
        {
            byte[] basic = [1, 128, 127, 128, 128, 0x48, 1, 0, 0, 0];
            check(PlayStationInput.TryDecode(0x054c, product, basic, out state) && state.Buttons == (PadButtons.B | PadButtons.Previous) && state.X == 0 && state.Y == 0, "Sony basic Bluetooth report maps common navigation layout");
            var padded = new byte[78]; basic.CopyTo(padded, 0);
            check(PlayStationInput.TryDecode(0x054c, product, padded, out state) && state.Buttons == (PadButtons.B | PadButtons.Previous), "Windows padded basic Bluetooth report is supported");
        }
        var ds4Padded = new byte[128]; ds4.CopyTo(ds4Padded, 0);
        check(PlayStationInput.TryDecode(0x054c, 0x09cc, ds4Padded, out _), "DS4 v2 128-byte standard report supported");
        // Synthetic fixtures have checksums generated independently with Python zlib, not the decoder.
        var ds4Bt = Convert.FromHexString("1180008080000028010000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000cfdd7a34");
        var ds5Bt = Convert.FromHexString("31008080000000000028010000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000008588fa24");
        check(PlayStationInput.TryDecode(0x054c, 0x05c4, ds4Bt, out state) && state.Buttons == (PadButtons.A | PadButtons.Previous), "DS4 enhanced Bluetooth report validates independent CRC fixture");
        check(PlayStationInput.TryDecode(0x054c, 0x0ce6, ds5Bt, out state) && state.Buttons == (PadButtons.A | PadButtons.Previous), "DualSense enhanced Bluetooth report validates independent CRC fixture");
        ds4Bt[7] ^= 0x20; ds5Bt[9] ^= 0x20;
        check(!PlayStationInput.TryDecode(0x054c, 0x05c4, ds4Bt, out _) && !PlayStationInput.TryDecode(0x054c, 0x0ce6, ds5Bt, out _), "Corrupt Bluetooth button data fails checksum before navigation");
        ds4Bt[7] ^= 0x20; ds4Bt[1] = 0;
        check(!PlayStationInput.TryDecode(0x054c, 0x05c4, ds4Bt, out _), "DS4 Bluetooth report without HID-data flag rejected");
        ds4[31] = 4; check(!PlayStationInput.TryDecode(0x054c, 0x09cc, ds4, out _), "DS4 disconnected state cannot produce actions"); ds4[31] = 0;
        var directions = new[] { PadButtons.Up, PadButtons.Up | PadButtons.Right, PadButtons.Right, PadButtons.Down | PadButtons.Right, PadButtons.Down, PadButtons.Down | PadButtons.Left, PadButtons.Left, PadButtons.Up | PadButtons.Left, PadButtons.None };
        for (var hat = 0; hat <= 8; hat++)
        {
            ds4[5] = (byte)hat; ds4[6] = 0;
            check(PlayStationInput.TryDecode(0x054c, 0x05c4, ds4, out state) && state.Buttons == directions[hat], "Sony D-pad hat " + hat + " maps correctly");
        }
        ds4[5] = 15; check(!PlayStationInput.TryDecode(0x054c, 0x05c4, ds4, out _), "Invalid hat cannot become neutral and arm input"); ds4[5] = 8;
        check(PlayStationInput.Axis(0) == -32768 && PlayStationInput.Axis(127) == 0 && PlayStationInput.Axis(128) == 0 && PlayStationInput.Axis(255) == 32767, "Native axes preserve extremes and both neutral center bytes");
        foreach (var length in new[] { 0, 1, 9, 11, 63, 65, 77, 79, 129 })
            check(!PlayStationInput.TryDecode(0x054c, 0x0ce6, new byte[length], out _), "Malformed Sony report length is rejected");
        var unknown = ds4.ToArray(); unknown[0] = 0xff;
        check(!PlayStationInput.TryDecode(0x054c, 0x05c4, unknown, out _), "Unrecognized report ID ignored");
        var batch = Batch(ds4, ds4);
        check(PlayStationInput.TryDecodeBatch(0x054c, 0x05c4, batch, out var parsed) && parsed.Length == 2, "RAWHID batch parses each complete report");
        check(!PlayStationInput.TryDecodeBatch(0x054c, 0x05c4, batch[..^1], out _), "Truncated raw-input batch rejected atomically");
        var overflow = batch.ToArray(); BinaryPrimitives.WriteUInt32LittleEndian(overflow, uint.MaxValue); BinaryPrimitives.WriteUInt32LittleEndian(overflow.AsSpan(4), uint.MaxValue);
        check(!PlayStationInput.TryDecodeBatch(0x054c, 0x05c4, overflow, out _), "Raw-input count multiplication is bounded without overflow");
        batch[^64] = 0xff;
        check(!PlayStationInput.TryDecodeBatch(0x054c, 0x05c4, batch, out parsed) && parsed.Length == 0, "Invalid report discards entire raw-input batch");

        var neutral = new PadState(0, 0, 0); var cross = new PadState(PadButtons.A, 0, 0); var time = TimeSpan.Zero;
        var xbox = new ControllerReading("xinput:0", cross, true); var router = new ControllerRouter();
        check(router.Read(xbox, time) == xbox, "XInput remains available without native devices");
        router.Connect("sony:one"); router.Receive("sony:one", cross, time);
        check(router.Read(xbox, time) is { Identity: "sony:one", Ready: false }, "Native connection suppresses XInput mirror and ignores held initial Cross");
        router.Receive("sony:one", neutral, time);
        check(router.Read(xbox, time) is { Identity: "sony:one", Ready: true, State.Buttons: PadButtons.None }, "Native input arms only after real neutral report");
        router.Receive("sony:one", cross, time); router.Receive("sony:one", neutral, time);
        check(router.Read(xbox, time)?.State.Buttons == PadButtons.A && router.Read(xbox, time)?.State.Buttons == PadButtons.None, "Short Cross press and release between timer ticks remain ordered");
        router.Receive("sony:one", new(PadButtons.Right, 0, 0), time);
        for (var i = 0; i < 1000; i++) router.Receive("sony:one", new(PadButtons.Right, 0, 0), time);
        check(router.Read(null, time) is { Ready: true, State.Buttons: PadButtons.Right }, "Duplicate high-rate reports do not flood the transition queue");
        router.Suspend(); router.Receive("sony:one", cross, time);
        check(router.Read(xbox, time) is { Ready: false }, "Inactive window or editing clears queued actions and requires release");
        router.Receive("sony:one", neutral, time); router.Read(null, time); router.Receive("sony:one", cross, time); router.Read(null, time);
        check(router.Read(xbox, TimeSpan.FromSeconds(3)) is { Ready: false, State.Buttons: PadButtons.None }, "Silent stale native input releases held state without falling back to duplicate XInput");
        router.Receive("sony:one", cross, TimeSpan.FromSeconds(3));
        check(router.Read(null, TimeSpan.FromSeconds(3)) is { Ready: false }, "Resumed held report after timeout cannot launch");
        time = TimeSpan.FromSeconds(3); router.Receive("sony:one", neutral, time); router.Read(null, time);
        router.Connect("sony:two"); router.Receive("sony:two", neutral, time); router.Receive("sony:two", cross, time);
        check(router.Read(null, time)?.Identity == "sony:one", "Multiple native controllers retain deterministic first-device ownership");
        router.Remove("sony:one");
        check(router.Read(xbox, time) is { Identity: "sony:two", Ready: false }, "Native handoff clears old queued input on the promoted controller");
        router.Receive("sony:two", neutral, time); router.Read(null, time); router.Remove("sony:two");
        check(router.Read(xbox, time) == xbox, "Native removal restores XInput fallback");
        router.Connect("sony:two"); router.Receive("sony:two", cross, time);
        check(router.Read(null, time) is { Ready: false }, "Reused device identity starts a new neutral-required session");
        router.Receive("sony:two", neutral, time); router.Read(null, time);
        for (var i = 0; i < 40; i++) router.Receive("sony:two", i % 2 == 0 ? cross : neutral, time);
        // A valid later neutral may rearm; no old Cross events may survive queue overflow.
        var afterOverflow = router.Read(null, time);
        check(afterOverflow is { State.Buttons: PadButtons.None }, "Transition-queue overflow discards backlog rather than replaying launches");
        var reducer = new GamepadNavigation(); router.Suspend(); router.Receive("sony:two", neutral, time);
        var first = router.Read(null, time)!.Value; reducer.Update(first.State.Buttons, first.State.X, first.State.Y, first.Ready, time);
        router.Receive("sony:two", cross, time); var accept = router.Read(null, time)!.Value;
        check(reducer.Update(accept.State.Buttons, accept.State.X, accept.State.Y, accept.Ready, time) == PadAction.Accept && reducer.Update(accept.State.Buttons, 0, 0, true, time) == PadAction.None, "Native report routing produces one direct-launch action per press");

        // A message-only HWND exercises the real Windows registration lifecycle; it never shows UI.
        Exception? failure = null; bool registered = false, secondRegistered = false;
        var thread = new Thread(() =>
        {
            try
            {
                using var window = new HwndSource(new HwndSourceParameters("ArcHive native input test") { ParentWindow = new IntPtr(-3), WindowStyle = 0, Width = 0, Height = 0 });
                var routing = new ControllerRouter(); var clock = Stopwatch.StartNew();
                using (var input = new NativePlayStationInput(window, routing, clock)) { registered = input.Registered; input.Dispose(); }
                using (var input = new NativePlayStationInput(window, routing, clock)) secondRegistered = input.Registered;
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(10))) throw new Exception("Raw input registration test timed out.");
        if (failure is not null) throw new Exception("Raw input registration test failed.", failure);
        check(registered && secondRegistered, "Windows Raw Input registers enumerates and unregisters on a hidden message-only window");
    }
    private static byte[] Batch(params byte[][] reports)
    {
        var bytes = new byte[8 + reports.Sum(r => r.Length)]; BinaryPrimitives.WriteUInt32LittleEndian(bytes, (uint)reports[0].Length); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), (uint)reports.Length);
        var offset = 8; foreach (var report in reports) { report.CopyTo(bytes, offset); offset += report.Length; } return bytes;
    }
}
