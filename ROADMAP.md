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

## V1 — complete the core workflow

| Work | Acceptance |
| --- | --- |
| Archive manager | Preview contents, expose per-archive sizes and source paths, extraction queue with cancellation and recovery after crashes. Existing manager filters games with archive associations and uses their detail actions. |
| Identification | Suggest launch candidates and engine with evidence; require selection, exclude uninstallers and tools; preserve manual choices. |
| Naming | Persist original name, canonical title, display title, version, platform, release channel and parser confidence separately. Preview suggested corrections; never rename files automatically. Support dates and ambiguous tokens through explicit review. |
| Progress/status | Delivered in 0.2.0; update availability is a separate release indicator. |
| Launch profiles | Delivered in 0.2.0. Native profiles work now; external adapters remain optional future integrations. |
| Metadata lookup | Implement optional Steam, IGDB, itch.io, VNDB and RAWG adapters using supported endpoints and configured credentials. Manual/offline mode requires no account. |
| Matching | Return candidates with stable provider IDs, title/alias/year/engine evidence and confidence. Ambiguous matches require review; never merge by title alone. |
| Reconciliation | Store provenance and retrieval time per field. Preview conflicts, retain manual overrides, allow per-field source choice and undo. |
| Artwork | Independent cover, backdrop and screenshot collections, async downloads, bounded disk cache, provider attribution and fallback artwork. Current local image is shared by card/detail. |
| UX polish | Full cinematic detail page, selected-card indicator, keyboard grid navigation, active navigation state, reduced-motion-aware transitions and high-DPI/accessibility validation. |

## V1.1 — organization and preservation

- Version history: local/manual tracking delivered in 0.2.0. Provider-supplied release lookup and polling remain planned; no automatic downloads or upgrades.
- Duplicate detection: candidate grouping by size and optional background hashes; distinguish exact copies from versions. Preview choices; never auto-delete.
- Batch operations: multi-select tagging/identification/extraction and previewed rename/relocate; cancellation, per-item results and recovery journal.
- Playtime: record sessions and totals; handle child processes, abnormal exits and user correction. Last-launched time is already present; elapsed playtime is not.
- Save backups: user-approved save locations, timestamped snapshots, retention, restore preview and pre-restore backup. Database backups are not game-save backups.
- Statistics, richer filters and collections; large-library performance benchmarks (10,000+ entries).

## V1.2 — optional integrations

- F95Zone: explicit opt-in, direct thread URL/ID association first; source-specific engine/developer/development status/channel/changelog fields. Confirm matches. Cached, rate-limited update checks with timestamps, backoff and manual refresh. No automatic game downloads. Verify API/backend terms before implementation; do not assume third-party cache access. Credentials belong in OS credential storage. F95Checker import should preserve source IDs and report skipped records.
- Locale Emulator: optional replaceable launch adapter with installation detection, per-game locale/codepage/timezone configuration and unsupported-platform handling. Do not bundle or install an emulator implicitly.
- Plugins: versioned provider/launch interfaces, explicit installation and capabilities, compatibility checks and failure isolation. No arbitrary plugin loading from scanned game folders.
- Custom metadata sources, Steam shortcut export, external utilities and portable configuration with path remapping.

## Release gates

The exact second approved logo has been supplied and integrated into the running app. The approved ICO is now packaged in the executable and Windows MSI. Regress scanning/extraction/database migration and edited metadata preservation. Exercise empty/missing/broken-art states, search, selection, resizing and cancellation. Document actual performance measurements separately from intended targets. All online adapters above are planned, not connected in this build.
