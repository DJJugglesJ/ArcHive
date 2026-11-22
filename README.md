# ArcHive

**From Archive to Adventure.** A local-first Windows library for standalone games downloaded as ZIP, RAR and 7z archives.

![Approved ArcHive branding](ArcHive.App/Assets/ArcHive-brand-approved.png)

Turn a folder of downloads into an artwork-rich collection. Keep versions together, choose how games launch, track your playtime and protect your saves.

Built with **C# / .NET 10 / WPF** and **SQLite**. Source version: **1.0.3**. [Published Windows releases](https://github.com/DJJugglesJ/ArcHive/releases) are listed separately from source builds.

## Your games, in one place

- **Browse by tag.** Click tag chips in game details to filter the library, see tag counts, and clear the active filter. Edit tags as removable chips or add several at once; review public F95Zone link metadata beside the tag list.
- **Discover and launch.** Recursively find archives and executable folders. A sole non-helper executable whose cleaned name matches its folder receives an editable automatic profile when details open. Existing choices are preserved. Keep multiple installations, versions and named launch profiles with individual arguments, working directories and defaults.
- **Inspect before extracting.** Preview ZIP/RAR/7z contents and issues, queue jobs, cancel safely and recover interrupted extraction without deleting originals.
- **Find information with confidence.** Review Steam, IGDB, itch.io, VNDB and RAWG candidates. Choose individual fields and artwork, preserve attribution and manual choices, and undo metadata changes. F95Zone appears in Find Game Information for public-thread descriptions, tags, engine and version/channel review.
- **Enjoy the artwork.** Covers, cinematic backdrops, screenshots and an artwork-rich grid use asynchronous bounded caches with offline reuse.
- **Browse your way.** Search display/canonical/original names, tags and collections. Combine collection, engine, controller, availability and unplayed filters; sort by title, playtime or recent play. Track Backlog, Playing, Completed and Abandoned independently of Played history.
- **Track time.** Record launch sessions and observed child processes, recover checkpointed durations and correct incomplete sessions manually.
- **Organize in batches.** Preview favorites, tags, status, collections, Played and NSFW changes. Queue archives together, review local identification, or preview same-drive file renames and relocation with a recovery journal.
- **Merge with evidence.** Find shared paths, possible title matches and byte-identical archives. Choose a primary game, retain conflicting source records and preserve launch profiles, sessions and save-backup ownership. No automatic deletion.
- **Protect progress.** Configure save folders, create snapshots, review restores and retain protected pre-restore backups. Regular snapshot retention is configurable.
- **Play from the couch.** Native DualSense/DS4 USB or Bluetooth and Xbox/XInput navigation; selectable button jewels, controller-support badges, context menus and an on-screen keyboard. Cross/A selects, Circle/B goes back, Triangle/Y opens context options, Options/Start opens the menu.
- **Remove an entry.** Open a game’s Options menu and choose Remove from Library. Confirmation defaults to Cancel. Original games, archives and saves remain on disk; rescanning a registered folder may rediscover the game.
- **Keep control.** A discreet 18 toggle manages manually classified NSFW visibility. Settings provides a confirmed library purge that retains game files and a recovery database.
- **Stay current.** Check GitHub releases in Settings > Updates, optionally check at startup, and download a verified installer before choosing to install. [Update workflow and limits](UPDATES.md).
- **Extend explicitly.** Configure an existing Locale Emulator installation or install and enable trusted metadata/launch plugins. [Integration contracts and limits](INTEGRATIONS.md).

## Run from source

Use Windows and the **.NET 10 SDK**. From the repository root:

```powershell
dotnet build ArcHive.App/ArcHive.App.csproj -c Release
dotnet run --project ArcHive.App/ArcHive.App.csproj -c Release
```

The first build restores NuGet dependencies. For an isolated library, set `ARCHIVE_DATA_DIRECTORY` before starting the app. By default, data lives in `%LOCALAPPDATA%\ArcHive`, separate from the installation.

## Your first game

1. Add a library folder and scan it. Rescans preserve edited metadata and never launch games automatically.
2. Select a game to open details. Use **Manage Archive…** to preview and queue extraction, or link an existing game folder.
3. In **Launch**, use **Scan Game Files** to review executable and engine evidence, or choose an executable manually. Save a launch profile and select its default.
4. Use **Find Game Information** to review a provider match and artwork, or edit the fields yourself.
5. Choose **Play**. Keep ArcHive open to record playtime; review or correct sessions through **Playtime & Naming…** in Overview.

Right-click or Triangle/Y opens game options. **Organize Library → Batch Tools…** contains duplicate merging, extraction, identification and file operations. **Browse…** controls additional filters and statistics. [Detailed 1.0 workflows](V1-WORKFLOWS.md).

## Metadata and privacy

Manual organization and launching need no account. Lookups and artwork selections are explicit; ArcHive does not download games automatically.

| Source | Lookup scope |
| --- | --- |
| Steam | App-ID/title catalog plus selected-match store descriptions/genres; optional owned-library import |
| IGDB | Title search and available cover, artwork and screenshots |
| itch.io | Your owned games or editable projects; not public catalog search |
| VNDB | Title search, metadata and available cover/screenshots |
| RAWG | Title search, artwork and explicit controller hints |
| F95Zone | Optional direct public-thread association and reviewed update evidence |
| GOG | Browser sign-in link and optional owned-library JSON import |

Credentials are session-only or saved through Windows Credential Manager, never in ArcHive library/settings files. Browser sign-ins remain in the browser. Plugins run as your Windows account and are not sandboxed; enable only distributions you trust. [Source settings](SOURCE-CONNECTIONS.md) · [Metadata matching](METADATA.md).

## Preservation and recovery

Database backups are separate from game-save snapshots. Extraction retains originals and uncertain interrupted output. Purging removes library records and roots, not games or saves. Duplicate merging preserves source records and backup ownership. File moves require explicit previews, same-drive destinations and content verification; collisions, overlapping paths and configured save folders are rejected. Retain pending journals when recovery requests manual review.

[Save backups](SAVE-BACKUPS.md) · [Extraction queue](ARCHIVE-QUEUE.md) · [Database migration](SQLITE-MIGRATION.md) · [File operations and purge](V1-WORKFLOWS.md).

## Build an installer

```powershell
powershell -NoProfile -File packaging/Build-Installer.ps1
```

This builds a self-contained Windows x64 application and per-user MSI, using the approved executable, shortcut and installer branding:

- `artifacts/publish/win-x64/ArcHive.exe`
- `artifacts/installer/ArcHive-1.0.1-win-x64.msi`

Packages are unsigned. Building packages does not publish a GitHub release. [Packaging prerequisites](packaging/README.md).

## Verification

```powershell
dotnet run --project ArcHive.Tests/ArcHive.Tests.csproj -c Release
```

The automated suite covers persistence/migration, safe extraction, source providers, credentials, controller parsing/routing, save restore, merge preservation, playtime, file recovery, Locale Emulator configuration and plugin subprocesses. The 1.0 milestone skips full end-to-end UI tests. Physical controllers, live provider availability and a real game under Locale Emulator are not verified by fixture tests. See [VALIDATION.md](VALIDATION.md) for current results and limits.

## Roadmap

[ROADMAP.md](ROADMAP.md) records changes by version and planned work, including additional source/engine coverage, scheduled backups and release checks, cross-drive relocation, portability and further performance/accessibility work.

Developed by Bellum Labs · Nicholas Gartin. © 2026 Bellum Labs. All rights reserved.

## Download queue (1.0.4)

Open **Downloads** beside Add Games. Paste a permanent public HTTPS file link and enter a ZIP, RAR or 7z filename, then choose **Add to Queue**. **Start / Retry Queue** processes entries one at a time. Stop cancels the active transfer; interrupted, cancelled and failed transfers restart from the beginning when retried.

Choose a ready archive and **Add to Library & Review** to verify its saved hash, add it to the library, and open the existing archive preview/extraction queue. Nothing extracts or launches automatically. Re-reviewing the same downloaded path reuses its library entry. Use duplicate review to merge with an existing game when appropriate.

This first integration accepts direct public files, not store pages, forum threads, checkout pages, signed query-string URLs or authenticated downloads. Use your browser for those flows and add the resulting local archive. No cookies or source credentials are used by the downloader. Public IPv4 HTTPS endpoints are supported; the size limit is 32 GiB per file. Files are stored under the ArcHive data directory's downloads folder; partial files are retained for recovery and overwritten on an explicit retry. Hash verification detects changes after download; it is not publisher authentication or a malware scan.

## Tag preferences and blacklists (next release)

Open **Tags & Blacklists** from the library toolbar. Search tags, sort them alphabetically, by usage or favorites first, and toggle favorite or blacklist status. Switch to **Games** to favorite or blacklist individual games, including those currently hidden by a tag rule. Save applies the draft; Cancel leaves it unchanged.

**Browse by tag** includes tag search and a favorite-tags filter. Blacklisted tags hide games with that exact tag; blacklisted games are tracked by stable game ID. Favorites never override a blacklist. Restore a rule in the same manager; a game hidden by a tag remains hidden until that tag rule is restored. No game files, metadata or source records are deleted. The existing NSFW visibility setting still applies.
