# Windows packaging

Requires Windows x64, .NET 10 SDK, Windows PowerShell 5.1 and NuGet access. WiX 4.0.6 is pinned and downloaded into ignored artifacts/tools; an existing compiler can be passed with -WixPath. This is the WiX installer compiler, unrelated to the website service.

```powershell
powershell -NoProfile -File packaging/Build-Installer.ps1
powershell -NoProfile -File packaging/Verify-Icons.ps1 -Installer artifacts/installer/ArcHive-0.2.0-win-x64.msi
```

The build exports a nine-frame ICO from the original approved board, publishes a self-contained win-x64 application, then produces a compressed per-user MSI. Default installation: %LOCALAPPDATA%/Programs/ArcHive. The MSI installs a Start Menu shortcut and an Installed Apps icon from the same ICO. No custom actions or game launches run during installation. Uninstall removes installed program files and shortcut, not the separate %LOCALAPPDATA%/ArcHive library.

The default release version is 0.2.0. Pass -Version X.Y.Z for later builds; the installer and executable versions must match, including when using -SkipPublish. Increment the version for every distributed change. Windows Installer may retain equal-version DLLs during repair, so do not distribute changed binaries under the same version. The stable UpgradeCode supports upgrades and rejects downgrades. The selected installation directory is stored in HKCU and recovered for maintenance/uninstall, including custom locations. Component GUIDs are derived from relative paths to remain stable across build machines.

Outputs under artifacts/ are not source-controlled. ArcHive.exe and the MSI are unsigned; production signing requires a publisher certificate. MSI files use Windows' generic package file-type icon; the approved identity is embedded in the application, Installed Apps entry and Start Menu shortcut.

The ICO exporter is a format-packaging script, not a redraw. The UI logo uses the original source with premultiplied alpha compositing; no generated replacement image ships.

Installer authoring follows the official [WiX package schema](https://docs.firegiant.com/wix/schema/wxs/package/) and [Windows Installer icon guidance](https://docs.firegiant.com/wix3/howtos/ui_and_localization/configure_arp_appearance/).
