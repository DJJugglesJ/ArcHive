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


## Remaining metadata providers — 0.4.0

- 233 automated checks pass (175 existing + 58 new provider/credential checks). Release WPF build succeeds with zero warnings and errors.
- Fixture HTTP tests exercise Steam cursor pagination/catalog reuse; itch.io short-page continuation, duplicate game IDs, repeated key detection and project scope; IGDB form authentication, escaped searches, expanded field mapping, token reuse and one bounded 401 renewal; RAWG encoding, field mapping and cache isolation.
- Shared transport tests cover missing credentials without network traffic, 401/403/503/redirect failures, rate-limit backoff, malformed and oversized responses, invalid IDs, host rejection, cancellation, credential-account cache isolation and error redaction. A malformed Steam catalog is rejected before caching and a subsequent corrected response succeeds.
- The complete suite ran in a normal Windows logon session. A uniquely named test credential round-tripped all settings through Windows Credential Manager, was replaced, survived an oversized failed replacement, and was deleted. Restricted execution cannot access that credential vault; it is not silently treated as a passing credential test. No existing user credentials were read or changed by the test.
- Catalog ranking now checks adjacent sorted scores for ambiguity instead of comparing every pair; provider fetch/parsing and catalog matching run off the UI thread. This is an implementation improvement, not a measured large-library UI benchmark.
- No real provider keys were supplied, so authenticated live-service availability was not tested. All network behavior was verified through deterministic fixtures. No real game library was changed. Full end-to-end UI tests were skipped as requested; installer rebuilding/install-uninstall testing was outside this milestone. Packaging versions align at 0.4.0.


## Provider artwork and gamepad milestone — 0.5.0

- 307 automated checks pass (233 existing + 74 artwork/controller checks). Release WPF build succeeds with zero warnings and errors. Tests link the actual WPF image decoder and XInput reader without opening app windows; full end-to-end UI tests were skipped as requested.
- Provider artwork tests cover role mapping, allowed CDN URLs, hostile URLs/image IDs, ownership checks, legacy image fallback, persisted independent roles and source identity. Controller tests cover unknown/partial badges, explicit provider review, manual provenance and undo.
- Real PNG/JPEG/BMP fixtures exercise decoding, dimension limits, malformed/truncated data and invalid BMP heights. Tests exposed a WPF FileFormatException path, now converted to an ordinary failed-image result. The test project now references the Windows desktop runtime needed for these decoder checks.
- Cache tests cover concurrent request deduplication, no authentication header, atomic publication, offline reuse across instances, disk eviction/redownload, corrupt-cache repair, cancellation, MIME/HTTP/size/decode/network failure and failure backoff. Fixtures do not contact image CDNs or use credentials.
- Simulated gamepad tests cover neutral arming, held-button suppression, cancellation, shoulders, timed directional repeat, dead zone/signed extremes, focus/busy/disconnect gating, responsive grid boundaries and separate confirmation presses. Native XInput polling tolerates unavailable controllers. Physical-controller navigation and launch behavior remain unverified; the existing controlled child-launch test remains green.
- The suite ran in the normal Windows session to retain the previous isolated Credential Manager regression tests, with temporary test credentials removed afterward. No user library or real game executable was used. Version numbers align at 0.5.0; installer rebuild/install-uninstall testing remains outside this milestone.


## Direct launch, controller profiles and visibility — 0.5.1

- 324 automated checks pass (307 previous + 17 preference/filter checks). Release build passes with zero warnings/errors. Direct launching retains the existing executable validation, exact argument-delivery regression and edge-triggered input tests; the confirmation window was removed.
- New checks cover DualSense/hidden defaults, profile mappings, persisted settings and backups, invalid/corrupt settings preservation, per-game NSFW SQLite round trips, unclassified legacy records and nonrepeating direct-launch input.
- Hiding is applied to the shared visible game list used by grid, archives, favorites, search and controller navigation; filtered-out selections are explicitly cleared. Physical controller behavior and full end-to-end UI tests were not run. PlayStation profiles change jewels/prompts and still require an XInput-compatible mapping for input.

## Native DualSense and DS4 — 0.6.0

- All 381 automated checks pass (324 existing plus 57 native controller checks). Release WPF build succeeds with zero warnings and errors.
- Synthetic USB, basic Bluetooth and extended Bluetooth report fixtures cover Sony IDs, layouts, axes, hats, buttons, CRC failures, truncation, batch bounds and atomic rejection. Routing tests cover short taps, duplicate reports, mapper suppression, neutral arming, focus suspension, stale input, queue overflow, multiple devices, reconnect/handoff and one launch action per press.
- A real hidden message-only Windows HWND verifies Raw Input registration, enumeration and disposal/re-registration. This is an automated interop check, not a full end-to-end UI test.
- Physical USB/Bluetooth controllers were unavailable for verification. Hardware/firmware compatibility and real controller navigation/launching remain unverified. The 0.5.1 mapper requirement above is historical and superseded by this backend.

## Duplicate detection and batch operations - 0.7.0

- All 430 automated checks pass (381 existing plus 49 organization checks); Release WPF build has zero warnings/errors.
- Tests distinguish shared references, title/version candidates and SHA-256 archive equality, including equal-size differing bytes, cancellation, empty scopes, unreadable/locked/changed paths and visible-scope isolation.
- All batch actions, whole-tag matching, manual provenance, stale/invalid requests, deep-copy immutability, launch-profile preservation, SQLite round trips, failure rollback, file retention and readable pre-change backup are covered.
- Eight tests instantiate the actual organizer without displaying it: multi-selection previews, selection/action/status/tag invalidation, empty selection and failed-save feedback. Full end-to-end UI tests were skipped as requested. Large-library timing and hostile filesystem races were not tested.

## Archive workflow and restart recovery - 0.8.0

- All 469 checks pass (430 existing + 39 archive/queue checks). Release WPF build succeeds with zero warnings/errors.
- Previews cover ZIP and RAR/7z fixtures, sizes, unsupported paths, duplicate/file-parent conflicts, entry/byte limits and cancellation.
- Queue tests cover pending reload, exclusive leases, duplicate jobs, write-failure atomicity, source locking/changes, sequential continuation, stop/retry, partial staging retention and fresh attempt paths. Durable journal snapshots reproduce interruption before output receipt and after library save; installation replay is idempotent. Link failures retain output, removed games are not recreated, and corrupt/future/mislocated journals fail closed.
- A stream-lifetime error discovered by the preview tests was corrected. Tests do not simulate hardware power failure or abrupt OS termination; full end-to-end UI tests were skipped. No live archive downloads or games were launched.

## Launch/engine suggestions and live UI - 0.9.0

- All 511 checks pass (469 existing + 42 detection/input/display checks); Release build has zero warnings/errors. Signatures, helper exclusions, conflict notes, traversal limits/cancellation, explicit application, default/argument/manual-metadata preservation, numeric-title context, stale files and accepted-evidence SQLite persistence are covered. Sony Triangle decoding and held-button suppression are verified with synthetic reports.
- Live desktop testing was performed this time using a separate fixture library. Card/archive right-click, shared navigation/selection, suggestion scan/apply, harmless child launch, organizer preview/apply and archive content preview passed. Database inspection confirmed the manual engine/default remained and a second profile/evidence were saved. Restart and small-window checks followed.
- The live test found missing dark backgrounds in secondary windows, a tooltip contrast problem and cramped evidence/source display; fixes were applied. Archive selection now updates the visible list rather than only its hidden selection model. See LAUNCH-DETECTION.md for the test flow and remaining hardware limits.

### Details-first release polish

Left-click or Cross/A opens a dedicated details page; Play is a separate action. Right-click or Triangle/Y opens options. Boolean controls use selected buttons with accessible checked state. Display paths retain the drive and final two components, with full paths available through tooltips and Copy full paths. Stored paths remain unchanged.

Live fixture verification confirmed opening details did not change the launch marker, explicit Play did, and the details layout fit a 1014-pixel-wide window. Automated checks cover compact drive/UNC paths, unchanged stored paths, and held-accept suppression.

## Save backups and restore — 0.10.0

- 551 automated checks pass (511 previous + 40 save-backup checks); Release application builds with zero warnings/errors.
- Coverage: nested files and empty directories, additions/replacements/removals, stale preview and post-preview snapshot tampering, protected undo snapshots, regular retention, invalid/overlapping/relative/root locations, size and entry limits, cancellation, missing/locked saves, missing payloads, directory symlinks, traversal manifests, concurrent operation locks, SQLite round-trip and legacy defaults.
- Interrupted-restore journals were simulated before and after displacing the original folder; recovery preserved the original data and was idempotent. These are deterministic filesystem tests, not physical power-loss or concurrent cloud-sync tests.
- Full end-to-end UI testing was deliberately skipped for this milestone. The 0.9.0 published release remains unchanged.

## Workflow and source settings — 0.11.0

595 checks pass (551 previous + 44 new). Coverage includes F95 canonical URL restrictions, query removal, redirect containment, page identity/challenge/size rejection, cancellation, credential-free requests, conservative parsing, field-level manual preservation; Steam/GOG normalization, import preview/apply isolation, deduplication, credential-field exclusion, Steam visibility failures; contextual game actions and SQLite/settings persistence. Source HTTP tests use synthetic responses, not live accounts. Release build has zero warnings/errors. Focused desktop layout checks covered the collection, details and expanded Settings in an isolated fixture. No full end-to-end or physical-controller test run was performed.


## 0.11.0 controller and release checks (2026-11-14)

- 621 automated checks passed. New coverage includes distinct Cross/Circle/Triangle/Options/Square mappings, neutral rearming across focus changes, keyboard editing/masking/cancellation, popup selector ownership and rejection of foreign native input targets.
- Release self-contained win-x64 publish and MSI build succeeded. All nine executable icon resources match the approved ICO; the MSI icon, Bellum Labs publisher and 0.11.0 version were verified.
- Isolated desktop checks used diagnostic input through the same controller route: collection to details, Triangle context menu, skip disabled actions, archive dialog, Options menu to Settings, shoulder focus traversal, dropdown cancel without closing Settings, text entry and discard, and nested About/back. About displayed 0.11.0 and the approved icon.
- MSI installed successfully into a dedicated test folder (exit 0). The installed executable launched, its application DLL matched the publish output, and the final controller keyboard theme was verified. Uninstall succeeded (exit 0), removed the installed executable and preserved the test library byte-for-byte. No user library or credentials were used for these checks.
- Physical controller hardware, firmware compatibility and controller-driven native file dialogs were not exercised live. Native file dialogs use guarded standard Windows keyboard input. External browsers and launched games retain their own input handling.

## ArcHive 1.0 source milestone — 2026-11-14

- Release app build: zero warnings and zero errors.
- Automated suite: 692 checks passed, including the existing Windows Credential Manager tests.
- New coverage: SQLite persistence of playtime/naming/collections/merge records; interrupted session recovery and manual correction; real parent/child process monitoring; merge conflict retention, stable launch/source IDs and old save-snapshot access; atomic batch queue failure; purge database/queue interruption boundaries; stale file previews, collision rejection, actual same-drive moves, rollback after database failure and committed recovery against SQLite; Locale Emulator XML/PE/profile argument rules; explicit plugin install/disable/enable/remove, output bounds, crashes, timeout, cancellation and hash tampering.
- The 10,000-game browse fixture verifies combined filters and playtime ordering. Measured at 6 ms in this Windows test run (timing is also printed by the runner); this is a core query measurement, not an end-to-end artwork/UI benchmark.
- No full end-to-end UI run for this milestone, as requested. No claim of physical-controller testing, live authenticated source testing or real Locale Emulator game execution. Plugin tests run isolated local fixture processes under the test account.
- Approved branding assets remain unchanged. Source version is 1.0.0; published release assets remain a separate release operation.


## Metadata corrections — 1.0.1 (2026-11-14)

- Release app build: zero warnings and errors. All 720 automated checks passed, including Windows Credential Manager checks.
- Added regression coverage for cleaned search names, F95 thread tags/Overview/named versions, optional release acceptance, manual-field preservation, provenance and SQLite round-trip; VNDB spoiler/lie filtering; RAWG and Steam selected-match details, identity validation, credential isolation, cancellation and malformed release evidence.
- The actual F95 HTTP adapter retrieved the supplied public thread 65211: Carnal Instinct, Unreal Engine, 38 header tags, a 652-character Overview, version v0.7.9 and channel Halloween. No authentication or challenge bypass was used. Synthetic test markup is committed; the downloaded page is not.
- No full end-to-end UI run or live authenticated Steam/IGDB/RAWG/itch.io account verification. Fixture checks do not guarantee catalog coverage or availability. F95 lookup requires a thread URL/ID, not a title-only search.
- Source is 1.0.1. Published v1.0.0 executable and installer assets are unchanged.


## My Sexy Neighbor 2 multi-source verification (2026-11-14)

- Actual authenticated Steam and RAWG title searches for My Sexy Neighbor 2 return the sequel first after normalizing Neighbor/Neighbour tokens for comparison. Source titles and sequel numbers are preserved. Steam app 3684710 and RAWG game 1015576 each score 0.8 (title-only heuristic, not probability).
- Steam selected details returned a 1,383-character description and three genres. RAWG selected details returned a 239-character description and three tags/genres.
- Actual F95 thread 279707 lookup returns My sexy Neighbour 2, Unity, 18 tags, a 240-character description and date-based version 2025-12-11. Completed status is removed from the proposed title. Date-based versions remain incomparable in update checks.
- itch.io authenticated owned-key lookup succeeds after accepting its observed empty-object terminal page. No title matches in the available owned-library scope. Nonempty objects remain errors. This is not an itch.io public catalog search.
- VNDB returns zero matches. IGDB was not configured, so its API was not tested.
- Automated suite: 729 checks passed. Release app build: zero warnings/errors. No full end-to-end UI test performed. Published installer unchanged.
- Opt-in diagnostic: `dotnet run --project ArcHive.Tests -c Release -- --providers-live "My Sexy Neighbor 2" 3684710`. Saved credentials are used only in memory; output includes result identities and field counts, never keys or descriptions. Optional final argument tests Steam details by ID. Focused diagnostics: `--itch-live` or `--rawg-live` followed by the title. These diagnostics never run as part of the automated suite.


## Controller and metadata workflow polish (2026-11-16)

- Release build succeeds without warnings/errors; 737 automated checks pass.
- New WPF controller regressions cover disabled/hidden dropdown choices, reverse traversal and boundaries, Circle restoring the opening selection, and Cross confirming changes. Source-default checks cover configured RAWG, Steam, existing enabled F95 associations, and credential-free VNDB fallback.
- Metadata search now offers Stop Search and Circle cancellation without leaving the dialog; edited queries cancel pending work and stale responses do not replace current results. Cross on a selected candidate transfers focus to field review, including after pending details finish.
- Focus returns after operations while ArcHive is active and after applying metadata in details. Launch setup moves focus to launch profiles.
- Desktop inventory confirmed another ArcHive installation is open. The single-instance guard prevents opening this build alongside it; the existing user session was left untouched. These changes have not had a live desktop acceptance run or physical-controller verification. Existing automated launch/process tests passed as part of the suite.
- Published installer remains unchanged.


## Application updater and live workflow verification (2026-11-16)

- Release build: zero warnings/errors. All 753 automated checks passed, including update release parsing, digest/size enforcement, downgrade prevention, foreign assets/redirect rejection, cancellation/partial cleanup, staged-file tampering, and startup preference persistence.
- PASS: live GitHub update check in Settings > Updates returned published 1.0.0 against source 1.0.1; Download and Install stayed disabled. Cross initiated the check. Circle returned through Updates, Settings and game details.
- PASS: controller diagnostic input follows the same routing as gamepads. Cross opened details and launched a harmless local fixture process; Triangle opened context options; Options opened the application menu and Settings; shoulder traversal and Circle dropdown dismissal worked. No physical gamepad was used.
- PASS: the fixture executable wrote its launch marker. ArcHive recorded one completed session of approximately 21 seconds and retained it through metadata saves and restart. No user game was launched or interrupted.
- PASS: live RAWG lookup returned the expected sequel first. Controller selection loaded details; Cross entered field review and shoulder navigation scrolled to tags. UI Apply persisted description, Simulation/Casual/Indie tags and release year while preserving the existing title.
- FIXED AND RETESTED: Triangle now offers Apply Reviewed Fields, Back to Matches, Choose Source and Return to Review, avoiding traversal through every artwork action. Applying with no chosen fields refuses to save; selecting only Title and applying through Triangle/Cross saved that field and preserved tags/session history. Focus returned to game options. Fresh dialogs now show an appropriate initial instruction.
- PASS: test library survived restart. SQLite integrity_check returned ok and foreign_key_check returned zero rows after both metadata saves. The isolated test app was closed normally; user library/settings were not used for writes. Saved RAWG credentials were used in memory for the authorized provider lookup.
- LIMITS: this is a focused desktop acceptance run, not all UI screens, physical-controller testing or a full game gameplay session. Active-search cancellation is automated-tested; the live RAWG request completed before the attempted Circle cancellation, which correctly returned to details. Newer-package installation through the updater is not live-tested because no newer packaged release exists. Download/hash/failure branches use deterministic synthetic package bytes and never execute them. No release assets were republished.


## Update readiness — 1.0.2 (2026-11-16)

- Release build passed; all 753 automated checks passed. NuGet vulnerability lookup emitted NU1900 because its service was unavailable.
- Settings now opens with a visible selected-tab focus. Live isolated-library checks passed for initial focus, shoulder traversal into ordering, Cross opening the dropdown, and Circle dismissing it. Controller diagnostics use the same routing as gamepads; no physical controller was used.
- Verified downloads disable redundant Download actions. Missing or changed staged installers clear readiness and require a fresh download.
- The real installed 1.0.1 library was backed up before testing. The live 1.0.1-to-1.0.2 installation results will be recorded with the release after publishing the stable package required by the old updater.

- Found in installed 1.0.1: updater lacked an explicit window background, leaving pale text on white. Set the approved dark background and foreground in 1.0.2.
