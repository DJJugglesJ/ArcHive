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

The 1.0.0 row describes the source implementation. Published installers are listed on [GitHub Releases](https://github.com/DJJugglesJ/ArcHive/releases). See [V1-WORKFLOWS.md](V1-WORKFLOWS.md) and [VALIDATION.md](VALIDATION.md) for supported workflows and verification evidence.

## Planned

- Broader local engine signatures and metadata source coverage.
- Provider release lookup and scheduled update checks with explicit review.
- Additional F95Zone source fields, authorized private-source access and F95Checker import.
- Automatic save-location discovery, scheduled backups and cloud backup destinations.
- Cross-drive relocation with copy verification and recovery; per-association cleanup.
- Archive decoder resource hardening and reviewed cleanup of retained extraction attempts.
- Portable configuration and path remapping, Steam shortcut export and external utility integrations.
- Additional plugin capabilities and publisher/distribution tooling.
- Cross-platform desktop support.
- Further large-library profiling, accessibility refinements and physical controller compatibility coverage.
