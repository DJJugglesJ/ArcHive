# Validation — November 14, 2026

.NET SDK 10.0.404 verified. SharpCompress 0.50.9 restored successfully.

84 checks passed on net10.0-windows. Tests cover original ZIP protections and persistence plus genuine RAR4/RAR5, solid RAR, LZMA/LZMA2 and solid/non-solid 7z. Each successful fixture's three output files were compared with original files using SHA-256. Byte limits, entry limits, cancellation cleanup, encrypted archive rejection and invalid format rejection passed. Generated 7z fixtures cover traversal, alternate streams, reserved names and trailing dots. Absolute-path rejection is covered by ZIP; the 7z writer normalizes drive prefixes, so it was not counted as an absolute-path fixture.

Tests never execute archived programs. Tests run with approved filesystem access because the restricted environment blocks staging-directory renames. No compatibility target override was used.

Manual GUI interaction and actual game launching were not verified. RAR-specific malicious links/traversal, multipart archives, checksum corruption, hostile filesystem races, crash recovery and decoder resource exhaustion require further fixtures and hardening. Passing checks do not constitute an exhaustive archive-security audit.

Run: dotnet run --project ArcHive.Tests/ArcHive.Tests.csproj

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
- Launched the verified UI using `dotnet ArcHive.App/bin/Release/net10.0-windows/ArcHive.App.dll`. Direct apphost launch did not produce a window in this execution environment; deployment/apphost launch remains to be checked on the target machine.
- Not tested here: 10k-entry performance, full keyboard navigation, high-DPI/minimum-size behavior, live extraction/launch via the redesigned controls, online providers (not implemented), and original branded icon (asset unavailable).

## Approved branding integration — 2026-11-14

- User-supplied original board is embedded unchanged, SHA256 recorded in design/DESIGN.md. Logo and icon use source-region resources rather than regenerated assets.
- Release build succeeded with zero warnings/errors. Live isolated 120-record preview showed the approved logo in navigation and approved arcade icon in the window title bar. Existing library data was not used or changed.
- Branding-only changes do not modify scanner, extraction or persistence behavior; the prior 99-check regression result remains applicable. Executable/installer ICO packaging is still pending.

## ArcHive 0.1.1 packaging and rename — 2026-11-14

- Renamed all current project folders, project files, namespaces, source references and documentation to ArcHive. Current staged tree contains no former-name matches (case-insensitive); historical Git commits remain intact.
- Clean self-contained Windows x64 publish succeeds. All 99 core regression checks pass after the rename.
- Original brand board retained. Logo is screen-composited from source pixels; no generated replacement ships. Live installed-app inspection verified the blended sidebar and correct window icon.
- Nine ICO frames (16/20/24/32/40/48/64/128/256) were compared byte-for-byte with all nine PE icon resources. MSI icon property/stream checked, and the installed shortcut's cached icon matched the source ICO hash.
- Final MSI clean-installed successfully into a custom test folder; all 406 installed payload files matched the published files by SHA256. The installed app launched and closed with exit code 0 using an isolated 120-entry test library.
- Uninstall exited 0, removed the complete custom installation folder and Start Menu shortcut, and left the saved library database hash unchanged.
- Tests exposed equal-version DLL retention and custom-path uninstall tracking. App/core/installer versions now align at 0.1.1; packaging rejects version mismatches and stores/reuses the installation directory. Increment versions for every distributed build. Future-version upgrade coverage and code signing remain release-hardening work.
- The existing local library was separately copied to the renamed data folder, its record count and SQLite integrity verified, and the original retained as a backup. No user data is committed.
- MSI SHA256: 14E3D49CC1DC95B35A059804F0FF48F6CE8176B9AA626C20C499FF1C6F0766C8. Installer is unsigned. The .msi file itself uses Windows' standard package icon; application, Installed Apps and Start Menu use the approved icon.


## Version/status/profile milestone — 0.2.0

- Release build passes with zero warnings/errors; 137 automated checks pass (99 existing + 38 milestone checks).
- Tests cover genuine schema-1 upgrades, permanent backup, schema/version preservation after forced migration failure, future-schema rejection, played/status separation, version provenance/history, separate local/installed releases, conservative comparison and side-by-side versions.
- Profile tests cover exact persistence, default ownership and rollback, profile deletion without legacy recreation, path containment, missing files/directories, unavailable adapters and null characters. A controlled child instance of the test executable verifies exact Unicode, empty, spaced, quoted and trailing-backslash arguments and working directory through ProcessStartInfo.ArgumentList. No downloaded game is launched.
- Original extraction/hash/SQLite regression tests remain green. New arguments use explicit editable rows rather than command-line parsing; empty rows represent empty arguments and literal whitespace is retained.
- Full end-to-end UI automation and installer install/uninstall tests were intentionally skipped for this milestone at the user's request to avoid unnecessary UI-test cost. No live user database was opened or migrated. Online version polling and external launch adapters remain unimplemented; latest-release metadata is manual/local.


## Metadata and confidence milestone — 0.3.0

- 175 automated checks pass (137 existing + 38 metadata checks); Release WPF build passes with zero warnings and errors.
- Deterministic matching tests cover aliases, Unicode, punctuation, sequel numbers, missing evidence, year/engine conflicts, ambiguous ties, duplicate identities and provider namespaces.
- Reconciliation tests cover preview immutability, selected fields only, manual provenance, timestamps, atomic stale-preview rejection, duplicate selections, protected undo, multi-field undo after a SQLite reload and legacy document compatibility.
- Fake HTTP transport verifies VNDB request shape, stable ID/date mapping, cache isolation, request cooldown, rate-limit backoff, malformed/oversized data, server errors, cancellation before search and during a request. No live provider availability is asserted; no secrets or user library are used.
- Full end-to-end UI tests were skipped as requested. The dialog is compiled with the Release app; interactive behavior is not claimed as end-to-end validated. Installer rebuilding/install-uninstall testing was outside this milestone. Version numbers are aligned at 0.3.0 for future packaging.
