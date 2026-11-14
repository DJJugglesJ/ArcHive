# ArcHive roadmap

Approved direction recovered from “Check ArcHive uniqueness” on 2026-11-14. This is the current plan; ARCHITECTURE.md remains a historical record. A roadmap entry is not a claim that its feature exists.

## Foundation — delivered

- C#/.NET 10 Windows desktop app. Retain the existing WPF shell rather than discard working code for Avalonia; the portable core remains separate. Reconsider Avalonia for a funded cross-platform milestone.
- Recursive discovery of ZIP/RAR/7z archives and folders containing executables; rescans preserve edited titles and metadata. No automatic execution or title-based merging.
- Staged extraction with path/entry/size checks, cancellation, unique destinations and retained originals. See README and VALIDATION for safety limits.
- SQLite library, legacy JSON migration, transaction rollback, database backup, multiple archives/installations per game, explicit executable selection.
- Favorites, played flag, tags, title/tag search, notes, local artwork, last-launched timestamp.
- ArcHive palette, navigation, responsive artwork grid with recycled rows, asynchronous bounded thumbnail decoding/cache, optional details panel and separate archive table.
- Conservative CamelCase/acronym/snake_case cleanup for new discoveries; explicit version/platform hints. Original paths stay unchanged. Hints are not authoritative release records.

## Version, status and launch milestone — delivered in 0.2.0

- Schema-2 migration with permanent schema-1 backup and transactional rollback; legacy played flags become Playing only when no explicit progress exists.
- Backlog / Playing / Completed / Abandoned, independent played history and availability; progress filtering and card/detail status.
- Per-archive and per-installation original name, version, platform and channel; filename hints initialize new locations without overwriting rescans. Latest-known release, source note, observation time and release history are persisted. Extraction carries the source archive release into the installation.
- Conservative numeric release comparisons and local/manual update indicators. Ambiguous suffixes or mismatched platforms/channels remain Unknown. Side-by-side newest installations prevent false update flags.
- Multiple named launch profiles per installation, persisted default, executable, argument list, working directory and reserved adapter ID. UI supports creation, editing, removal, default selection and executable browsing. Exact command/working-directory confirmation precedes every launch; arguments are passed without a shell.

## Metadata and confidence milestone — delivered in 0.3.0

- Provider contract and validated, bounded offline JSON import; optional explicit VNDB title search with stable IDs, short-lived memory cache, cancellation, timeout, size limits and rate-limit backoff.
- Deterministic title/alias similarity plus optional year/engine evidence; conflicts lower scores, close candidates are flagged ambiguous, provider IDs remain separate. Scores are heuristics, not probabilities; all matches require review.
- Field-level preview and opt-in replacement, manual provenance, source IDs/retrieval timestamps, persisted undo with subsequent-edit protection. Schema 2 stores additive metadata inside the existing game document; existing databases need no structural migration.
- Review dialog and release year/engine editors. No automatic selection, merge, artwork retrieval, background traffic or release polling. Steam, IGDB, itch.io, RAWG and F95Zone adapters remain planned.

## Additional metadata providers — delivered in 0.4.0

- Steam supported catalog lookup (stable app IDs/titles), IGDB title search with app-token acquisition/renewal, RAWG title search, and itch.io owned-game / editable-project matching. Provider scopes are explicitly labeled; itch.io does not offer public title search through the documented account API.
- Provider selector, user-supplied API credential settings, optional Windows Credential Manager storage, session-only mode and Forget. Credentials do not enter library records or provider errors.
- Bounded/cancellable HTTP, per-provider request pacing and rate-limit backoff, session caches, complete catalog pagination with progress checks, source backlinks and unchanged field-by-field reconciliation. Steam/itch catalog ranking runs off the UI thread.
- Fixture-based automated provider tests and real isolated Windows credential-store tests. Live authenticated API availability and full UI behavior are not claimed by these checks.

## Artwork and gamepad milestone — delivered in 0.5.0

- Provider-sourced cover/backdrop/screenshot candidates, explicit preview and per-role selection, independent persisted choices with attribution, screenshot removal and legacy local-image fallback.
- Asynchronous credential-free PNG/JPEG downloads, validated dimensions and actual decoding, atomic publication, disk/memory bounds, offline cache reuse, failure backoff, cancellation and recycled-container protection.
- Editable Unknown/None/Partial/Full controller support; badges for Partial/Full and reviewable RAWG controller hints when explicitly present.
- Active-window XInput navigation with dead zones, directional repeat, selection highlight, shoulder view switching and controller-operable launch confirmation. No input injection into games. Real hardware/full UI verification remains outstanding.

## Controller profiles and discreet visibility — delivered in 0.5.1

- Direct mouse/gamepad launching without confirmation; executable/profile checks and held-button suppression remain.
- Persistent DualSense (default), DualShock 4 and Xbox visual prompt profiles, vector button jewels in settings/launch/cards, and persistent gamepad navigation preference. Native PlayStation input was subsequently added in 0.6.0.
- Small crossed-out 18 / 18+ control for hiding/showing manually classified NSFW games. Persisted setting defaults to hidden; filters all library views and clears filtered selections without deleting records.

## V1 — complete the core workflow

| Work | Acceptance |
| --- | --- |
| Archive manager | Delivered in 0.8.0: content/size preview, persistent sequential queue, cancellation and explicit restart recovery. Partial/uncertain output is retained; automated cleanup and decoder resource hardening remain planned. |
| Identification | Delivered in 0.9.0: bounded local candidate/engine review, observed evidence, common-helper exclusion and opt-in profile/engine/title application preserving defaults. Broader signatures remain planned. |
| Naming | Persist original name, canonical title, display title, version, platform, release channel and parser confidence separately. Preview suggested corrections; never rename files automatically. Support dates and ambiguous tokens through explicit review. |
| Progress/status | Delivered in 0.2.0; update availability is a separate release indicator. |
| Launch profiles | Delivered in 0.2.0. Native profiles work now; external adapters remain optional future integrations. |
| Metadata lookup | Delivered in 0.4.0 with supported scopes documented in METADATA.md. Manual/offline mode requires no account. |
| Matching | Delivered in 0.3.0: stable IDs, title/alias/year/engine evidence and heuristic confidence, explicit review. |
| Reconciliation | Delivered in 0.3.0 for title, description, tags, release year and engine; per-field selection, provenance and protected undo. |
| Artwork | Delivered in 0.5.0 for supported PNG/JPEG provider assets; independent roles, async bounded cache, attribution and local fallback. Steam catalog artwork is unavailable through the chosen supported endpoint. |
| UX polish | Full cinematic detail page, selected-card indicator, keyboard grid navigation, active navigation state, reduced-motion-aware transitions and high-DPI/accessibility validation. |

## V1.1 — organization and preservation

- Version history: local/manual tracking delivered in 0.2.0. Provider-supplied release lookup and polling remain planned; no automatic downloads or upgrades.
- Duplicate review delivered in 0.7.0: shared paths, possible title/version matches, optional size-prefiltered SHA-256 archive comparison, cancellable background work and per-file warnings. No automatic deletion. Merge/reconciliation and per-association cleanup remain planned.
- Batch organization delivered in 0.7.0: multi-select favorites/status/tags/NSFW and library-record removal with per-entry before/after preview and transactional save. Batch identification/extraction, previewed rename/relocate and recovery journal remain planned.
- Playtime: record sessions and totals; handle child processes, abnormal exits and user correction. Last-launched time is already present; elapsed playtime is not.
- Save backups delivered in 0.10.0: explicit save folders, timestamped snapshots, configurable regular retention, restore preview, protected pre-restore backup and interrupted-swap recovery. Automatic discovery/scheduling and cloud backups remain future work. Database backups are not game-save backups.
- Statistics, richer filters and collections; large-library performance benchmarks (10,000+ entries).

## V1.2 — optional integrations

- F95Zone: explicit opt-in, direct thread URL/ID association first; source-specific engine/developer/development status/channel/changelog fields. Confirm matches. Cached, rate-limited update checks with timestamps, backoff and manual refresh. No automatic game downloads. Verify API/backend terms before implementation; do not assume third-party cache access. Credentials belong in OS credential storage. F95Checker import should preserve source IDs and report skipped records.
- Locale Emulator: optional replaceable launch adapter with installation detection, per-game locale/codepage/timezone configuration and unsupported-platform handling. Do not bundle or install an emulator implicitly.
- Plugins: versioned provider/launch interfaces, explicit installation and capabilities, compatibility checks and failure isolation. No arbitrary plugin loading from scanned game folders.
- Custom metadata sources, Steam shortcut export, external utilities and portable configuration with path remapping.

## Release gates

The exact second approved logo has been supplied and integrated into the running app. The approved ICO is now packaged in the executable and Windows MSI. Regress scanning/extraction/database migration and edited metadata preservation. Exercise empty/missing/broken-art states, search, selection, resizing and cancellation. Document actual performance measurements separately from intended targets. VNDB, Steam, IGDB, itch.io and RAWG connect only on explicit lookup with their documented scopes. F95Zone remains planned.

## Native PlayStation input — delivered in 0.6.0

- Windows Raw Input for standard DualSense and DS4 v1/v2 over USB/Bluetooth, with no XInput mapper required for ArcHive navigation and direct launch.
- Bounded report parsing, Bluetooth CRC validation, queued short taps, native priority over XInput mirrors, device arrival/removal, and neutral rearming after focus loss, handoff or stale input.
- 381 automated checks and a clean Release build; physical-controller testing remains outstanding. No game input injection or advanced haptics.

## Duplicate review and batch organization - delivered in 0.7.0

- Organize dialog scoped to the current filtered view; extended multi-selection and evidence-driven duplicate review. Title candidates remain separate from shared-path and exact-archive evidence.
- Optional cancellable background archive hashing, unchanged original files, bounded streaming and warnings for unavailable/changed locations.
- Explicit per-item batch previews, invalidation when selection/arguments change, manual tag provenance, whole-transaction persistence and pre-save database backup. Library removal has an additional confirmation and preserves disk files.
- 430 automated checks including dialog component tests; clean Release build. Full end-to-end UI tests skipped. See [ORGANIZATION.md](ORGANIZATION.md) for scope and limits.

## Archive previews, queue and recovery - delivered in 0.8.0

- Background ZIP/RAR/7z directory previews with paths, declared sizes, entry issues and policy limits.
- Durable sequential extraction jobs, source-change checks, exclusive queue ownership, cancellable runs and explicit retry in new attempt folders.
- Running-to-Interrupted startup recovery, retained uncertain output, durable Extracted receipts and idempotent installation linking after library-save interruptions. No automatic filesystem deletion or decoder resume.
- 469 automated checks; journal snapshots exercise interruption boundaries. Hardware power-loss and full UI automation are not claimed. See ARCHIVE-QUEUE.md.

## Launch detection and selection polish - delivered in 0.9.0

- Local executable/engine suggestions with evidence and explicit application; no automatic launching, default replacement or metadata overwrite. Accepted evidence persists.
- Numeric-only new discoveries include immediate-parent context without removing the identifier; existing titles remain unchanged. Full sources and source-folder access are exposed.
- Right-click / Triangle / Y options, shared keyboard/controller navigation, synchronized archive-row highlight, stronger card/table selection, explicit dark secondary windows and themed menus/dropdowns/tooltips.
- 511 automated checks, clean Release build, and live desktop fixture test covering suggestions, preserved-default launch, batches, archive preview and selection. Physical controller hardware remains unverified. See LAUNCH-DETECTION.md.

### Details-first release polish

Left-click or Cross/A opens a dedicated details page; Play is a separate action. Right-click or Triangle/Y opens options. Boolean controls use selected buttons with accessible checked state. Display paths retain the drive and final two components, with full paths available through tooltips and Copy full paths. Stored paths remain unchanged.

Live fixture verification confirmed opening details did not change the launch marker, explicit Play did, and the details layout fit a 1014-pixel-wide window. Automated checks cover compact drive/UNC paths, unchanged stored paths, and held-accept suppression.

## Save backups and restore — delivered in 0.10.0

Per-game save-folder configuration persists in SQLite. Background snapshot/restore work validates content hashes and rejects links, overlapping backup locations, stale previews and corrupted snapshots. Folder replacement is journaled; pre-restore snapshots and displaced folders remain available. 551 automated checks pass; Release build has zero warnings/errors. Full end-to-end UI tests were skipped as requested. See [SAVE-BACKUPS.md](SAVE-BACKUPS.md).
