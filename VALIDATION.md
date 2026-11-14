# Validation — November 14, 2026

.NET SDK 10.0.404 verified. SharpCompress 0.50.9 restored successfully.

84 checks passed on net10.0-windows. Tests cover original ZIP protections and persistence plus genuine RAR4/RAR5, solid RAR, LZMA/LZMA2 and solid/non-solid 7z. Each successful fixture's three output files were compared with original files using SHA-256. Byte limits, entry limits, cancellation cleanup, encrypted archive rejection and invalid format rejection passed. Generated 7z fixtures cover traversal, alternate streams, reserved names and trailing dots. Absolute-path rejection is covered by ZIP; the 7z writer normalizes drive prefixes, so it was not counted as an absolute-path fixture.

Tests never execute archived programs. Tests run with approved filesystem access because the restricted environment blocks staging-directory renames. No compatibility target override was used.

Manual GUI interaction and actual game launching were not verified. RAR-specific malicious links/traversal, multipart archives, checksum corruption, hostile filesystem races, crash recovery and decoder resource exhaustion require further fixtures and hardening. Passing checks do not constitute an exhaustive archive-security audit.

Run: dotnet run --project GameShelf.Tests/GameShelf.Tests.csproj

## Live GUI verification — November 14, 2026

Tested the Release WPF app using desktop input and screenshots with disposable fixtures:
- Add folder and recursive discovery of ZIP/RAR/7z plus executable folder.
- Tag editing, favorite flag and favorites-only filter.
- ZIP extraction, executable picker, launch confirmation, actual harmless test executable launch (verified launched.txt), and automatic played flag.
- Cover import and visible preview; description editing.
- RAR5 and solid 7z extraction with availability-state updates.
- Restart restored extraction paths, metadata, cover and played state.
- Rescan retained four entries and metadata without duplicates.
- Manual linking to a separate test folder persisted its selected path.
- Empty-search results disabled actions and now clear the detail panel.

Fixed: moved action buttons into a fixed toolbar above scrolling details, reduced cover height, cleared stale details on deselection, and displayed filtered/total entry counts. Rebuilt on .NET 10 with zero warnings/errors; all 84 regression checks passed again.

The application is left open with disposable test entries. Real user game folders were not scanned or launched. Test data lives under work/gui-fixtures, work/gui-extracted and work/gui-linked. Only the purpose-built TestGame executable was launched, not third-party archive fixture executables.

Remaining coverage: this is a primary-workflow GUI pass, not an exhaustive UI audit. Overlap-merge dialogs, cancellation timing through the UI, accessibility/screen-reader use, DPI/resizing combinations, and crash-recovery behavior are not manually verified. Cancellation and unsafe-path handling retain automated coverage.

## ArcHive continuation validation — 2026-11-14

- Release app build: succeeded with 0 warnings and 0 errors.
- Console regression suite: all 99 checks passed, including 5 new filename-parser checks and existing archive safety, RAR/7z fixture hashes, SQLite migration/rollback/backup and metadata preservation checks.
- Initial suite attempt could not publish a staged directory inside the restricted system temporary folder. Rerunning with TEMP/TMP under workspace work/test-temp passed; no extraction checks were removed.
- Live WPF verification used ARCHIVE_DATA_DIRECTORY=work/archive-ui and 120 synthetic records. This did not touch the default user library. Existing fixture JPEGs are test artwork, not branding or game covers.
- Observed: 120-item library renders with artwork/fallbacks and recycled rows (16 card controls exposed in the initial accessibility tree), selection populates matching details, archive manager shows 60 matching records, search for 001 yields one record, inspector toggle hides detail controls. Visual inspection caught and corrected an implicit Window background style issue; rebuilt and verified the obsidian surface.
- Launched the verified UI using `dotnet GameShelf.App/bin/Release/net10.0-windows/GameShelf.App.dll`. Direct apphost launch did not produce a window in this execution environment; deployment/apphost launch remains to be checked on the target machine.
- Not tested here: 10k-entry performance, full keyboard navigation, high-DPI/minimum-size behavior, live extraction/launch via the redesigned controls, online providers (not implemented), and original branded icon (asset unavailable).

## Approved branding integration — 2026-11-14

- User-supplied original board is embedded unchanged, SHA256 recorded in design/DESIGN.md. Logo and icon use source-region resources rather than regenerated assets.
- Release build succeeded with zero warnings/errors. Live isolated 120-record preview showed the approved logo in navigation and approved arcade icon in the window title bar. Existing library data was not used or changed.
- Branding-only changes do not modify scanner, extraction or persistence behavior; the prior 99-check regression result remains applicable. Executable/installer ICO packaging is still pending.
