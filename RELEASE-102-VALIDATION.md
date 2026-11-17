# ArcHive release and follow-up verification

## Completed 1.0.1 → 1.0.2 installation verification
- The installed 1.0.1 app discovered and verified the actual published MSI. Initial installation from its AppData staging folder failed with error 2755 (logged retry: 1619/path not found).
- Repeating the full updater workflow using a workspace library copy succeeded. Windows Installer recorded ArcHive 1.0.2 installation status 0. This was an actual updater-driven installation, not just an installer launch.
- All 406 installed files match the published output. Installed executable version: 1.0.2, source 22b19aa6356bd6231740ce61e6099a43a3da50cd. The installed app opened the original library successfully.
- Original library records and settings retained. Artwork and screenshots match pre-upgrade hashes. SQLite integrity check: ok; foreign-key check: no violations. Existing 1.0.1 normalized older records with default fields before installation; settings gained only the default CheckForUpdatesOnStartup field. Backup files rotated normally.
- No game files were removed or relocated by the upgrade.

## Follow-up source changes (1.0.3; not included in these assets)
- Update downloads use Windows temporary storage, with a selectable alternative folder and installer logging. Windows Installer successfully read/extracted the published package from the new default staging folder (status 0); this was an access test, not another upgrade.
- Direct Remove from Library confirmation, active-session protection, larger grouped game options, favorites/metadata/launch shortcuts, and selected-card focus restoration.
- Release build: zero warnings/errors. All 758 automated checks pass. Essential desktop retest passed: Triangle opens the corrected menu; removal confirmation defaults to Cancel; confirmed removal changes only the isolated library, retains game files, returns to browsing, and passes SQLite checks. The updater displays its temporary staging location and Change control correctly.
- Controller tests used diagnostic input through the real controller routing; no physical gamepad was tested. The published v1.0.2 MSI and ZIP have not been replaced.
