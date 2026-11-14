# ArcHive

**From archive to adventure.** A local-first Windows library for standalone games downloaded as ZIP, RAR and 7z archives.

![ArcHive approved branding](ArcHive.App/Assets/ArcHive-brand-approved.png)

Turn a folder of downloads into an artwork-rich collection. Find your games, keep multiple versions together, choose how they launch, and organize your backlog without moving the original files.

Built with **C# / .NET 10 / WPF**, backed by **SQLite**. Current source version: **0.9.0**.

## Your collection, in one place

- **Preview and queue archives.** Inspect ZIP/RAR/7z contents, sizes and entry issues. Queue sequential extractions, stop safely, and recover interrupted jobs with fresh retries and idempotent library linking.
- **Choose with evidence.** Review launch candidates and engine hints from local files, exclude common helpers, and apply only checked suggestions. Existing launch defaults and manual metadata stay intact unless explicitly changed.
- **Discover and launch.** Recursively scan folders for archives and executables. Extract ZIP/RAR/7z into a new game folder, link existing installations, and create named launch profiles with individual arguments and working directories.
- **Make it yours.** Browse a responsive artwork grid, favorites and a separate archive view. Search titles/tags, edit notes, track Backlog / Playing / Completed / Abandoned, and use the optional details panel.
- **Keep versions straight.** Store multiple archives and installations per entry, release/platform/channel details, latest-known releases and observation history. Numeric comparisons help identify local update gaps.
- **Enrich with confidence.** Review metadata candidates from VNDB, Steam, IGDB, itch.io and RAWG. Matching uses title/alias, year and engine evidence; choose fields individually and preserve source attribution. Protected undo is available for text metadata.
- **Bring the artwork.** Select provider covers, backdrops and screenshots, or use local images. Background downloads and bounded disk/memory caches keep the library responsive and reuse cached artwork offline.
- **Play from the couch.** Navigate and launch with native DualSense/DS4 USB or Bluetooth input, or Xbox through XInput. Select DualSense, DS4 or Xbox button jewels. Cards display controller support when marked Partial or Full.
- **Organize in batches.** Multi-select entries, preview changes, then update favorites, progress, tags or NSFW classification. Remove unwanted library records while keeping original archives and game folders.
- **Review duplicates.** Find shared paths and possible title/version matches. Optional background SHA-256 comparison identifies byte-identical archives, with cancellation and per-file warnings.
- **Keep visibility discreet.** A small crossed-out 18 / 18+ toggle hides or shows manually marked NSFW entries. Hidden is the default; unclassified games remain visible.

## Run ArcHive

### From source

Use Windows and the **.NET 10 SDK**. Run these commands from the repository root; the first build restores dependencies from NuGet:

```powershell
dotnet build ArcHive.App/ArcHive.App.csproj -c Release
dotnet run --project ArcHive.App/ArcHive.App.csproj -c Release
```

### Build a Windows installer

```powershell
powershell -NoProfile -File packaging/Build-Installer.ps1
```

This produces a self-contained Windows x64 executable and per-user MSI:

- `artifacts/publish/win-x64/ArcHive.exe`
- `artifacts/installer/ArcHive-0.9.0-win-x64.msi`

The installed app needs no separate .NET runtime. The executable, Start Menu shortcut and Installed Apps entry carry the approved ArcHive icon. Packages are currently unsigned; these are build outputs, not a claim that downloadable releases are published. See [packaging prerequisites and details](packaging/README.md).

## Your first game

1. Choose **Add library folder** and select a folder containing your downloads or installed games. Rescans preserve edited metadata and do not launch anything.
2. Select an entry. For an archive, choose **Preview / queue**, inspect the contents, and **Add to queue** with a destination parent. **Run pending / finish linking** extracts to a unique folder and retains the original archive. For an existing installation, use **Add folder** if needed.
3. Choose **Launch / engine suggestions** to inspect local evidence and explicitly add a profile, or use **Files and launch setup** and **Choose executable**. Add a named profile, arguments and an optional working directory. Save your changes.
4. Choose **Find metadata** to review a provider match and artwork, or edit the details manually.
5. Left-click a game or press Cross/A to open its details page, then choose **Play game**. The selected profile launches after validation. Played history and last launch time are recorded independently of progress status.

Right-click a card/archive row or press Triangle/Y on the selected game to open options. Shift+F10 also opens options. Full source locations appear in details and tables, even when the display title is numeric. See [launch suggestions and controls](LAUNCH-DETECTION.md).

Controller input pauses while ArcHive is inactive, busy or editing a field. Release the controls after connecting or returning to the app. Native Sony input takes priority over XInput mirrors; profiles change the displayed symbols independently of the detected device. ArcHive does not map input into games. See [supported controllers](NATIVE-CONTROLLERS.md).

## Archive queue and recovery

Use **Extraction queue** to review jobs across restarts. Jobs run only when requested. Stop leaves later jobs pending; interrupted jobs need an explicit Retry and use fresh folders. Completed output awaiting library linking can finish without extracting again. Select a job to inspect its output and retained attempt paths. Uncertain files from crashes are retained for review. See [archive workflow and recovery](ARCHIVE-QUEUE.md).

## Clean up a growing library

Use search, status, favorites, archive view and visibility controls to narrow the library, then choose **Organize**. Only entries in that current view appear in the organizer.

- Select rows with Ctrl/Shift, or **Select all shown**.
- Choose **Find duplicates**. Enable **Compare archive contents (SHA-256)** for byte comparisons; this reads same-size archive candidates and can take time for large downloads.
- Select an evidence group to review its titles and paths. **PossibleTitle** means a review candidate, often different releases; **SharedPath** means multiple references to one location; **ExactArchive** means matching size and SHA-256 at scan time.
- Select the entries you want to change, choose an action, and **Preview changes**. Review each before/after value, then **Apply preview**. Changing selection or arguments invalidates the preview.

Removal requires an additional confirmation and removes library records and their metadata only. Files remain on disk and rescans may rediscover them. There is no automatic merge, file deletion or folder-content comparison. See [duplicate and batch behavior](ORGANIZATION.md).

## Metadata, privacy and storage

Manual organization and launching require no provider account. Metadata searches and artwork choices are explicit; there is no automatic game downloading or release polling.

| Provider | Current lookup scope |
| --- | --- |
| VNDB | Title search, metadata and available covers/screenshots |
| Steam | Supported app-ID/title catalog; no guessed artwork URLs |
| IGDB | Title search and available cover/artwork/screenshots; your app credentials |
| itch.io | Your owned games or editable projects and available covers; not public catalog search |
| RAWG | Title search, available background/screenshots and explicit controller hints; your API key |

Matching scores are review aids, not guarantees. Configure your own credentials in **Provider settings**, optionally stored in Windows Credential Manager. They are not saved in library records. Consult [METADATA.md](METADATA.md) for provider scopes, attribution, import format and reconciliation rules.

The default data folder is `%LOCALAPPDATA%\ArcHive`, separate from installed program files. SQLite stores the library; settings, cached artwork and local images stay alongside it. Saves retain a rolling database backup. Existing libraries migrate with dedicated migration backups; see [migration and recovery](SQLITE-MIGRATION.md). Database backups are not game-save backups. For an isolated development library, set `ARCHIVE_DATA_DIRECTORY` before starting the app.

## Roadmap

| Delivered | Next / planned |
| --- | --- |
| Discovery, archive previews, persistent extraction queue and restart recovery | Broader corruption testing, decoder resource limits and orphan cleanup |
| SQLite, favorites, tags and search | Richer collections and filtering |
| Versions, progress, launch profiles and evidence-based launch/engine suggestions | Broader engine signatures and richer filename review |
| Five metadata providers, confidence matching and reconciliation | Provider release polling and optional F95Zone thread associations |
| Artwork selection, asynchronous caching and controller navigation | Broader image formats, accessibility and large-library benchmarks |
| Duplicate review and transactional batch organization | Duplicate reconciliation/merge, batch identification/extraction and previewed relocation |
| Last-launched history | Session playtime, game-save backups and restore |
| Native launch profiles | Optional Locale Emulator adapter and versioned plugins |

[ROADMAP.md](ROADMAP.md) tracks delivered milestones and acceptance criteria. Windows WPF remains the current implementation; cross-platform Avalonia is a future consideration.

## Verification and current limits

Run the automated suite:

```powershell
dotnet run --project ArcHive.Tests/ArcHive.Tests.csproj -c Release
```

**511 checks pass**, including launch/engine suggestions, manual-choice preservation, Triangle input, archive recovery, duplicates and transactional batch edits. The Release build passes with zero warnings/errors. A live end-to-end desktop test exercised detection/apply/launch, organizer batches, archive preview, selection, options and restart using an isolated fixture library. Real controller hardware and authenticated live provider behavior remain unverified. See [VALIDATION.md](VALIDATION.md) for the evidence and limits.

Extraction stages files without overwriting existing games and checks paths, entry counts and declared/actual output sizes. Encrypted/incomplete archives are unsupported. Extraction is not a malware sandbox; use trusted downloads and user-controlled destinations. Decoder CPU/memory limits, hardware power-loss testing, orphan cleanup and broader corruption/link-metadata testing remain hardening work. Artwork currently supports the documented PNG/JPEG provider paths. Version tracking is local/manual, not automatic online updating.

## Project guide

- [Launch detection and UI checks](LAUNCH-DETECTION.md) · [Archive queue and recovery](ARCHIVE-QUEUE.md) · [Organization](ORGANIZATION.md) · [Metadata](METADATA.md) · [Artwork and gamepads](ARTWORK-GAMEPAD.md)
- [Native controllers](NATIVE-CONTROLLERS.md) · [Design and branding](design/DESIGN.md)
- [Roadmap](ROADMAP.md) · [Validation](VALIDATION.md) · [Windows packaging](packaging/README.md)
- `ArcHive.App` — WPF interface, artwork presentation and Windows input
- `ArcHive.Core` — scanning, extraction, storage, providers, launch plans and organization
- `ArcHive.Tests` — automated regression suite and provider fixtures
