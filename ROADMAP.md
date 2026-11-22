# ArcHive roadmap

## Version history

| Version | Changes |
| --- | --- |
| 0.1.0 | Windows desktop foundation; recursive ZIP/RAR/7z and executable discovery; safe staged extraction; SQLite library and JSON migration; favorites, tags, notes and search; approved ArcHive artwork and branding. |
| 0.2.0 | Per-location versions, platforms and release channels; independent play status; multiple launch profiles with arguments, working directories and defaults. |
| 0.3.0 | Metadata provider contract, VNDB and offline JSON; confidence matching, field-level review, provenance and protected undo. |
| 0.4.0 | Steam, IGDB, itch.io and RAWG metadata integrations; bounded requests and Windows Credential Manager storage. |
| 0.5.0 | Provider artwork, cover/backdrop/screenshots, asynchronous artwork caching, controller support badges and XInput navigation. |
| 0.5.1 | Direct launching, DualSense/DS4/Xbox prompt profiles and discreet NSFW visibility. |
| 0.6.0 | Native DualSense and DS4 input over USB/Bluetooth, held-input suppression and disconnect handling. |
| 0.7.0 | Duplicate evidence review and optional archive hashing; previewed batch favorites, status, tags, NSFW classification and library removal. |
| 0.8.0 | Archive content previews, persistent extraction queue, cancellation, retained interrupted output and restart recovery. |
| 0.9.0 | Launch-candidate and engine evidence; right-click/controller context menus; details-first navigation; compact paths; UI polish, About and branded executable/installer release. |
| 0.10.0 | Explicit save folders, local snapshots, regular snapshot retention, restore previews, protected pre-restore snapshots and interrupted-restore recovery. |
| 0.11.0 | Source settings, browser sign-in links, optional owned-library import, public F95Zone thread association/update checks, controller-accessible dialogs and menus, branded release. |
| 1.0.0 | Optional Locale Emulator profiles and explicit process-based metadata/launch plugins; consolidated UI terminology; library purge with recovery database; playtime sessions and correction; duplicate merging with retained source records and backup ownership; collections, filters and statistics; reviewed canonical/display naming; batch collection/played changes, extraction, local identification, file rename/relocation and recovery journals. |
| 1.0.1 | Metadata corrections: VNDB tags, selected-match RAWG/Steam detail retrieval, richer IGDB tags, cleaned search titles, integrated F95Zone thread metadata/version review; controller review shortcuts and focus improvements; GitHub release update checks, verified installer downloads and explicit install handoff. |
| 1.0.2 | Readable dark updater window; Settings initial controller focus; clearer verified-download state and recovery from missing or altered staged installers; branded updater validation release. |
| 1.0.3 | Separate temporary updater staging, alternative download-folder selection and installer logs; direct confirmed library removal with active-session protection; larger grouped game options and selected-entry focus restoration; clickable tag filters and chip editing; direct reviewed F95 link import with additional header tag parsing; conservative automatic launch profiles for matching single-executable folders. |
| 1.0.4 | Public HTTPS archive queue; cancellation and restart recovery; signature/hash review; explicit library and extraction handoff; responsive progress and cancellable verification. |

The 1.0.0 row describes the source implementation. Published installers are listed on [GitHub Releases](https://github.com/DJJugglesJ/ArcHive/releases). See [V1-WORKFLOWS.md](V1-WORKFLOWS.md) and [VALIDATION.md](VALIDATION.md) for supported workflows and verification evidence.

## Planned — consolidated scope (planning revision 1.7)

This is a **forward-looking planning revision**, not an ArcHive application release. The version-history table above documents shipped source changes through 1.0.4. Items below are planned unless separately recorded as implemented and verified. Do not interpret proposed APIs, layouts, or integrations as already shipping.

**Product objective:** A local-first, artwork-rich standalone-game library that can discover, download, organize, preserve, launch and play games from a controller-first experience. Desktop mode and fully offline Library operation remain first-class.

**Priority:** P0 foundational; P1 committed development target; P2 follow-on; P3 exploratory/deferred. Phase numbers indicate dependency order, not necessarily separate releases. UI work can progress in parallel.

### Architectural invariants and acceptance gates (P0)

- Preserve the current C# / .NET 10 / WPF / SQLite application and existing provider, controller, archive, backup and plugin functionality; evolve rather than rewrite.
- Use stable game identity independent of filenames, titles and directories. Distinguish catalog identity, library entry, installation, version, original archive, source association and launch profile. Same-named unrelated games must not silently merge; alternative titles and provider IDs must be retained.
- Use versioned SQLite migrations with recovery backups. Preserve user metadata, provider attribution, existing launch choices, play history and backup ownership across rescans, moves, imports and updates.
- Make all destructive filesystem changes previewable, journaled and recoverable. No silent deletion of originals, saves, older installations or active torrent data. Verify archive extraction boundaries, disk space, collisions and installation identity.
- Background tasks must support bounded concurrency, cancellation, persistence, restart recovery and structured errors. Credentials and passkeys must not be written to application logs or catalog data.
- Keep existing working features and previously planned capabilities unless explicitly marked removed below.

### Phase 1 — Library intelligence and managed storage (P0/P1)

- **ARCH-LIB-01 — Personal ratings/reviews (P1):** Store a nullable personal 0.5–5 star rating, personal review and timestamps; display separately from provider scores; preserve through rescans and moves. Support sorting and filters.
- **ARCH-LIB-02 — Smart naming (P1):** Parse CamelCase, punctuation, release-group/platform suffixes, build numbers and irregular versions; retain original names and parsed evidence; offer previewable naming templates, collision checks and manual overrides. A proposed title never becomes the primary identity.
- **ARCH-LIB-03 — Archive inspection and duplicates (P1):** ZIP/RAR/7z manifest inspection, integrity checks and nested-root detection; duplicate candidates by name/size/manifests, confirmed identical archives by streaming hashes; prevent traversal, dangerous links, excessive output and overwrite.
- **ARCH-LIB-04 — Executable detection (P1):** Rank executable candidates from filename, PE metadata, engine layout and helper/installer exclusions; preserve manually chosen launch profiles. Support multiple executable/renderer/architecture profiles.
- **ARCH-LIB-05 — Version-aware library storage (P0):** Default newly managed game layout:

      <Game>/
        installed/
          <version>/
        archives/
        backups/      (optional)
        userdata/     (optional)

  The approved **default update behavior is "Ask each time"**: offer *Install alongside*, *Replace after safeguards*, or *Cancel*. Never force existing external installations into this layout. Each installed version retains its own executable, arguments, working directory, controller profile and compatibility settings. Treat userdata relocation as optional, only for validated game-specific paths.
- **ARCH-LIB-06 — Versioned launching (P0):** Normal **Launch/Play starts the newest installed version** (using verified version ordering, not lexicographic folder sorting). A Library game context menu offers **Launch Other Version** when multiple installations exist, listing selectable versions. Selecting an older version for one session does not change the default. The Versions tab can launch specific versions. Allow manual ordering when version strings cannot be compared reliably.
- **ARCH-LIB-07 — Archive cleanup (P1):** Approved **default: "Ask after successful extraction"**. Options: *Delete Archive*, *Keep Archive*, *Always Keep for This Game*. Only mark an archive eligible after verified successful extraction/installation and ownership matching, with no pending rollback/extraction. Respect active torrent seeding and retention policies: never delete data still needed by an active torrent without resolving/confirming its removal. External archives require explicit authorization. Provide a **Storage Cleanup** view inside **Organize Library** for eligible archives, redundant verified duplicates and abandoned temporary files; do not automatically remove older installed versions.
- **Acceptance gate:** One game can own multiple installed versions and archives; new and external installations coexist; correct version launches; manually selected executables and personal data persist; cleanup never relies on title similarity alone.

### Phase 2 — Local Game Catalog and enrichment (P1; discovery UI P2)

- **ARCH-CAT-01 — Local-first Catalog:** SQLite records can represent discovered games **without adding them to the Library**. Store stable canonical IDs, developer, alternative names, tags, controller-support status (unknown/unsupported/partial/full), release date, media, source associations and timestamps. No central ArcHive cloud catalog.
- **ARCH-CAT-02 — Search and aliases:** Indexed title/developer/alias/tag search (consider SQLite FTS5), alphabetical browsing, multiple same-name records and user-overridable identity matches.
- **ARCH-CAT-03/04 — Enrichment and reconciliation:** Retain field-level provenance, retrieval times, conflicting provider claims and source-specific version/rating history; merge only when provider IDs or strong supporting evidence justify it. Avoid indiscriminate crawling and low-confidence auto-merges. Initial population occurs through explicit lookup, import, source association or watched entries; optional background enrichment follows provider rules.
- **ARCH-CAT-05/06 (P2):** Optional Watchlist, catalog discovery view and bounded local artwork/media caching.
- **Acceptance gate:** A game discovered on multiple sites can reuse verified metadata later when downloaded, while distinct games with the same title remain distinct; Catalog and Library identities are separable and work offline.

### Phase 3 — Source providers, accounts and update monitoring (P1)

- **ARCH-SRC-01 — Capability-based provider contract:** Search, Game Information, ratings, versions, authorization and downloads are separately declared optional capabilities. Enforce caching, rate limits, timeouts, cancellation and provider-failure isolation.
- **ARCH-SRC-02 — F95Zone and itch.io:** Retain current F95Zone public-thread information/version review and existing itch.io owned/editable project integration; extend only through supported site/API capabilities. Do not assume public catalog search, private access or direct downloads are universally available. Respect provider access rules.
- **ARCH-SRC-03 — Ratings and versions:** Preserve original scales, vote counts, dates, URLs and source attribution. Version checks retain raw and parsed strings; display incomparable/unknown states rather than guessing. Keep provider ratings distinct from personal ratings.
- **ARCH-SRC-04 — Authentication:** OAuth/system browser where provided; isolated WebView2 source sessions where appropriate; Windows Credential Manager for secrets. Support account disconnect and token redaction; no login challenge/CAPTCHA bypass.
- **ARCH-SRC-05 — RPDL:** Optional provider for permitted torrent discovery, account integration and matching releases by verified F95Zone thread ID. Protect private torrent passkeys; do not expose them in logs/exports. Downloads remain subject to site authorization and applicable rights.
- **Previously planned source work remains:** Broader source/engine coverage, provider release lookups and scheduled update checks, extra F95Zone fields, authorized private-source access when supported, and F95Checker import.

### Phase 4 — Unified downloads, BitTorrent and updates (P1; advanced reuse P2)

Implemented foundation in 1.0.4: permanent public HTTPS archive links, a sequential persistent queue, stop/retry from the beginning, completed-file hash review and explicit extraction handoff. The broader capabilities below remain planned.

- **ARCH-DL-01 — Native HTTP/HTTPS queue:** Persistent queue, pause/retry/cancel, concurrency/bandwidth limits, verified range-resume when supported, ETag/Last-Modified and size validation, partial-file verification, disk-space checks and restart recovery. Do not concatenate bytes when a remote object changes.
- **ARCH-DL-02 — Embedded source browser:** Optional WebView2 discovery/authentication view with controller navigation and external-browser fallback. Native transfers take over only supported downloads; expiring links, provider permissions and authentication constraints must be respected.
- **ARCH-DL-03 — Download-to-game matching:** Exact provider IDs may link automatically; title/developer/version similarity produces reviewable suggestions; never silently match from filenames alone.
- **ARCH-DL-04 — Staged extraction and updates:** Validate, stage, inspect, back up verified data, journal commit, verify and recover. Default **Ask each time** whether to install alongside, replace safely or cancel. **Automatic save migration only for verified game-specific save locations**; otherwise preserve the old installation and request user review.
- **ARCH-DL-05 — BitTorrent:** Magnet links and .torrent files, file selection, piece verification, resume, peer/progress statistics, bandwidth limits, configurable seeding and persistent session state. Select a maintainable native/managed torrent engine after prototype validation. Treat torrent files and downloaded data as separate retention concerns.
- **ARCH-DL-06 — Torrent-assisted updates (KEEP; P2):** Reuse existing verified file/piece content where torrent metadata and file mapping permit; recheck data and download missing or changed pieces. Integrate with safe staged installation, never unverified binary patching or destructive in-place modification.
- **Acceptance gate:** Interrupted downloads and updates recover safely; torrent seeding and cleanup do not conflict; source-matched downloads do not corrupt an unrelated game.

### Phase 5 — Premium UI and organization (P1)

- Keep approved black/charcoal/neon-green terminal/arcade-hive branding while refining the desktop look with inspiration from XLibrary/GameShelf, not copying their assets/UI wholesale.
- Virtualized artwork-rich Library grid plus compact and details-list modes; bounded image loading and large-library search/filter performance.
- Game details retain **Overview · Media · Versions · Launch · Sources · Backups**. Surface personal and source ratings separately, version-specific profiles, installation/update state and linked source pages.
- Smart collections as saved queries (Favorites, Recently Played, Unplayed, Completed, Abandoned, Needs Extraction, Updates Available, etc.) and advanced tags, source, controller and status filters.
- Keyboard command palette (proposed Ctrl+K), shared commands for menus/controller, responsive focus and reduced-motion behavior.

### Phase 6 — Controller-first experience and mapping (P1; fullscreen refinements P2)

- **ARCH-INPUT-01–05:** Detect XInput, DualSense/DS4 and supported generic controllers without regressing existing native navigation. Offer per-game/per-version controller-to-keyboard/mouse mapping profiles: WASD, mouse, buttons, analog thresholds, sensitivity, dead zones and layered/modifier actions. Provide a visual mapping editor and export/import later.
- Automatically activate a selected profile for the launched game, suppress mapped inputs while the overlay has focus where feasible, and release simulated keys on focus loss, disconnect, exit or crash. Prefer native controller input unless mapping is enabled. Use non-invasive OS-supported input synthesis; do not promise compatibility with protected/anti-cheat games.
- **ARCH-INPUT-06–08:** Full controller access to Library, Archives, Downloads, Sources, settings, context menus and text entry. Add Big Picture-inspired full-screen Controller Mode and controller virtual keyboard/mouse navigation, reusing core commands and views.

### Phase 7 — Supported-mode overlay and extension host (P1/P2)

- **ARCH-OVR-01–04:** Separate overlay host and IPC; controller summon/close shortcut, quick menu, game/session info, controller profiles and safe input ownership. Begin with verified windowed/borderless support; exclusive fullscreen and anti-cheat-protected games are **not guaranteed**. Overlay must not assume games can be paused automatically.
- **ARCH-EXT-01/02:** Small versioned local extension API for commands, panels and game lifecycle events, with explicit permissions, isolation where viable, opt-in activation and crash containment. Keep the existing trusted metadata/launch plugin support; expand without forcing a marketplace.
- **ARCH-OVR/EXT follow-ons (P2):** Screenshots, controller-operated web browser, **media-session controls for an existing player rather than a dedicated ArcHive music player**, and optional **Discord Rich Presence only** (not a Discord chat/voice client).
- **Save Backups remain** as ordinary save-file copies, previews and restore; **emulator and generic process save states are removed**.
- **P3 deferred:** Built-in offline memory editor/trainer research with explicit permissions and game-specific restrictions; optional extension distribution/marketplace. Neither is required for initial overlay or plugin functionality.

### Existing planned commitments retained

The planning consolidation does **not** cancel these pre-existing ROADMAP items:

- Broader local engine signatures and metadata source coverage.
- Provider release lookup and scheduled update checks with explicit review.
- Additional F95Zone fields, authorized private-source access and F95Checker import.
- Automatic save-location discovery **for verified games**, scheduled backups and optional cloud **backup destinations** (distinct from the removed cloud **Catalog**).
- Cross-drive relocation with copy verification/recovery and per-association cleanup.
- Archive decoder resource hardening and reviewed cleanup of retained extraction attempts.
- Portable configuration/path remapping, Steam shortcut export and external utility integrations.
- Additional plugin capabilities and publisher/distribution tooling, **excluding the deferred marketplace**.
- Cross-platform desktop support as a long-term effort, without an immediate WPF rewrite commitment.
- Large-library profiling, accessibility refinements and physical-controller compatibility coverage.

### Explicitly removed, restricted or deferred

| Item | Decision |
| --- | --- |
| Generic and emulator save-state implementation | **Removed**; keep existing Save Backups |
| Built-in memory editor | **Deferred P3**; do not include in initial core |
| Dedicated ArcHive music player | **Removed**; replace with existing-player media controls |
| Full Discord client | **Removed**; optional Rich Presence retained |
| Extension marketplace | **Deferred P3**; local extension loading retained |
| Torrent-assisted updates | **Retained P2**; not removed |
| AI-generated game recommendations | **Removed**; normal rating, tags, sorting and filters remain |
| Universal game-overlay compatibility | **Restricted** to validated display/input modes |
| Automatic save migration | **Restricted** to verified game-specific paths |
| Cloud/shared ArcHive Catalog | **Removed**; local SQLite Catalog retained |
| Cloud save-backup destinations | **Retained as previously planned**; separate from cloud Catalog |

### Branding and approved UI vocabulary — authoritative

- Brand: **ArcHive**; **From Archive to Adventure.**; **Bellum Labs · Nicholas Gartin**. Retain the approved black/neon-green hacker-arcade hive logo/icon.
- Main navigation: **Library**, **Archives**, **Organize Library**; content label **NSFW**.
- Fields: **Play Status** (including **Abandoned**); **Installation Status**.
- Actions: **Find Game Information**, **Manage Archive…**, **Scan Game Files**, **Open Game Folder**, **Launch Other Version**.
- Save-management label: **Save Backups**. Use **Metadata** only in technical settings, not as a primary action.
- Empty state: **Your library is empty.** / **Add a library folder to discover your games and archives.**
- Details tabs: **Overview · Media · Versions · Launch · Sources · Backups**.

### Validation and release discipline

- Validate schema migration against legacy databases and ensure old version history/installed features remain intact.
- Test same-title disambiguation, arbitrary version formats, parallel installations, wrong-executable avoidance, safe cleanup, torrent seeding retention, interrupted file moves, download resume, corrupted archives and source outages.
- Verify physical controllers, mapped-key cleanup, overlay focus, keyboard-only accessibility, and functioning offline mode.
- Ship no automated destructive changes without recovery and explicit user-approved rules. Documentation updates do not imply implementation or release.

## Implemented since 1.0.4 (next release)

- Searchable tag browsing; alphabetical, most-used and favorites-first tag ordering.
- Favorite tags and games; persistent exact-tag and game-ID blacklists with an explicit restore manager.
- Draft editing with Save/Cancel; blacklist rules hide entries without deleting files or metadata.
