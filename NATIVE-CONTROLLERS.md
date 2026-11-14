# Native PlayStation controllers (0.6.0)

ArcHive reads Sony controllers through Windows Raw Input. Connect by USB or pair using Windows Bluetooth settings, enable gamepad navigation, and release the controls once. No XInput mapper is required for navigating ArcHive or launching a selected game. Games must provide their own controller compatibility; ArcHive does not translate or inject input into games.

| Controller | Sony vendor/product ID | Report layouts |
| --- | --- | --- |
| DualShock 4 v1 / v2 | 054c:05c4 / 054c:09cc | 01 basic/USB (10, 64, 78 or 128 bytes), extended Bluetooth 11–19 (78 bytes) |
| Standard DualSense | 054c:0ce6 | 01 USB (64 bytes), 01 basic Bluetooth (10 or padded 78 bytes), 31 extended Bluetooth (78 bytes) |

DualSense Edge, third-party clones and dedicated wireless adapters are not claimed as supported. Basic Bluetooth works without sending feature/output reports to enable extended mode. Extended Bluetooth packets require a valid HIDP-prefixed CRC32.

D-pad/left stick moves selection, Cross launches, Circle returns focus to the gallery, and L1/R1 switch library views. DualSense/DS4/Xbox settings select button artwork independently of device detection. Touchpad, gyro, rumble, adaptive triggers, audio and other advanced features are outside this navigation backend.

Native Sony input has priority while a recognized device is connected, including over an XInput mirror exposed by a mapper. The first registered native device owns navigation until removed. This deliberately prevents simultaneous Xbox control while that Sony device is connected. A mapper that hides the physical HID device can still supply XInput instead. Registration failure leaves XInput available and adds a hint in the navigation area.

Input is foreground-only. Editing, busy operations and inactive windows suspend navigation. Real neutral input is required on connect/reconnect, focus return, device handoff, invalid packets, queue overflow or a two-second report timeout. A stale recognized device remains the owner until removal instead of handing a held action to a possible XInput mirror. Changed navigation states are queued (maximum 32) so short taps between timer ticks survive; excessive backlog is discarded. Registration is removed when the window closes.

## Verification

381 automated checks pass, including synthetic transport reports and routing tests. A real hidden Windows message-only window validates registration/enumeration/disposal. No full end-to-end UI tests were run. Physical DualSense/DS4 hardware was not tested; real device and firmware compatibility still needs confirmation.

## Protocol references

The small navigation-only parser is independently written against the report layouts documented by the SDL implementations: [DualSense](https://github.com/libsdl-org/SDL/blob/main/src/joystick/hidapi/SDL_hidapi_ps5.c) and [DS4](https://github.com/libsdl-org/SDL/blob/main/src/joystick/hidapi/SDL_hidapi_ps4.c). Windows integration follows Microsoft [Raw Input](https://learn.microsoft.com/en-us/windows/win32/inputdev/about-raw-input), [registration](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerrawinputdevices) and [report retrieval](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getrawinputdata).
