# Native PlayStation controllers (0.11.0)

ArcHive reads Sony controllers through Windows Raw Input. Connect by USB or pair using Windows Bluetooth settings, enable gamepad navigation, and release the controls once. No XInput mapper is required for navigating ArcHive or launching a selected game. Games must provide their own controller compatibility; ArcHive does not translate or inject input into games.

| Controller | Sony vendor/product ID | Report layouts |
| --- | --- | --- |
| DualShock 4 v1 / v2 | 054c:05c4 / 054c:09cc | 01 basic/USB (10, 64, 78 or 128 bytes), extended Bluetooth 11–19 (78 bytes) |
| Standard DualSense | 054c:0ce6 | 01 USB (64 bytes), 01 basic Bluetooth (10 or padded 78 bytes), 31 extended Bluetooth (78 bytes) |

DualSense Edge, third-party clones and dedicated wireless adapters are not claimed as supported. Basic Bluetooth works without sending feature/output reports to enable extended mode. Extended Bluetooth packets require a valid HIDP-prefixed CRC32.

| Action | DualSense / DS4 | Xbox |
| --- | --- | --- |
| Select / activate; edit a text field | Cross (X) | A |
| Cancel / back; close a dropdown first | Circle | B |
| Selected game context options | Triangle | Y |
| App options menu | Options / Start | Menu / Start |
| Previous / next control | L1 / R1 | LB / RB |
| Navigate lists, tabs, sliders, keyboard | D-pad / left stick | D-pad / left stick |
| Open text keyboard | Square | X |

The same controller route covers main screens, modal dialogs, provider fields, metadata reviews, archive queues, organization and save backups. Shoulders traverse enabled visible controls, scrolling the focused control into view. Cross opens details from the collection; Play remains an explicit action on details. The Options menu reaches library views, imports, activity, organization and Settings. Triangle opens game context options; on other screens without a context menu it opens screen options. Extended-selection lists preserve selection while arrows move focus; Cross toggles the focused selection.

Editable fields open an in-app keyboard with letters, numbers, symbols, spaces, new lines, uppercase, backspace, clear, Use text and Cancel. Passwords are masked; cancelling discards the edit buffer. Native Windows file/confirmation dialogs receive standard keyboard actions only while the foreground window belongs to ArcHive: shoulders Tab/Shift+Tab, Cross Enter, Circle Escape, Triangle context menu, Options F10. Square opens text entry for the currently focused native field. Browser sign-in pages and launched games are external applications and are not controlled by ArcHive.

DualSense/DS4/Xbox settings select button artwork independently of device detection. Touchpad, gyro, rumble, adaptive triggers, audio and other advanced features are outside this navigation backend.

Native Sony input has priority while a recognized device is connected, including over an XInput mirror exposed by a mapper. The first registered native device owns navigation until removed. This deliberately prevents simultaneous Xbox control while that Sony device is connected. A mapper that hides the physical HID device can still supply XInput instead. Registration failure leaves XInput available and adds a hint in the navigation area.

Input is foreground-only. Inactive applications suspend navigation. Editing and modal windows stay navigable; disabled actions remain disabled during work. Real neutral input is required on connect/reconnect, focus return, device handoff, invalid packets, queue overflow or a two-second report timeout. A stale recognized device remains the owner until removal instead of handing a held action to a possible XInput mirror. Changed navigation states are queued (maximum 32) so short taps between timer ticks survive; excessive backlog is discarded. Registration is removed when the window closes.

## Verification

621 automated checks pass, including synthetic USB/Bluetooth reports, distinct face/menu mappings, neutral rearming, text-entry masking/cancellation, popup ownership and routing tests. A real hidden Windows message-only window validates registration/enumeration/disposal. Focused desktop checks exercise the shared controller route using diagnostic keys with an isolated library. Physical DualSense/DS4 hardware and firmware compatibility remain unverified.

For repeatable UI checks only, set both `ARCHIVE_CONTROLLER_TEST_INPUT=1` and an isolated `ARCHIVE_DATA_DIRECTORY`. F1/F2/F3/F4 map to select/back/context/menu, F5 to text entry, F6/F7 to previous/next control, and F8/F9/F11/F12 to up/down/left/right. These are ignored in normal launches. Native Windows dialogs still require physical controller input or their standard keyboard controls.

## Protocol references

The small navigation-only parser is independently written against the report layouts documented by the SDL implementations: [DualSense](https://github.com/libsdl-org/SDL/blob/main/src/joystick/hidapi/SDL_hidapi_ps5.c) and [DS4](https://github.com/libsdl-org/SDL/blob/main/src/joystick/hidapi/SDL_hidapi_ps4.c). Windows integration follows Microsoft [Raw Input](https://learn.microsoft.com/en-us/windows/win32/inputdev/about-raw-input), [registration](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerrawinputdevices) and [report retrieval](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getrawinputdata).
