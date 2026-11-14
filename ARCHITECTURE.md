# Architecture and incremental implementation

## Product shape

The main view has a searchable library and a selected game's details. The library should eventually offer cards/list modes, filters for archive-only, extracted, missing and played, and a dedicated extraction queue. Details should expose cover, screenshots, notes, tags, source archives, installed folders, executable candidates and a clearly labeled Play action.

The current list/detail UI implements the smallest useful path from discovery to remembering and launching a game. No executables run as a consequence of scanning or extracting.

## Boundaries

```text
WPF view / current controller -> library model and application operations
                             -> scanner (discovery only)
                             -> repository (versioned JSON today)
                             -> IArchiveExtractor (ZIP adapter today)
                             -> explicit process launch

Next: view models + commands -> application services -> repository and adapters
```

`GameShelf.Core` contains storage, scan merge policy, filesystem path checks and ZIP extraction. `GameShelf.App` owns the Windows dialogs, UI thread, operation cancellation, cover import and explicit launch. `GameShelf.Tests` exercises the core with disposable synthetic files; it never launches a downloaded program.

Stable game IDs identify user metadata. Paths identify discovered files, not game identity across moves. Extraction only records a folder after successful promotion; failure does not set an extracted flag. State is derived from file/folder presence, so manually deleted content is not silently considered available. The current “Extracted / folder” label describes availability, not verified completeness or archive provenance.

## Target persistent model

Use SQLite through Microsoft.Data.Sqlite when multi-location games and migration become necessary. Introduce repository interfaces before changing the storage implementation.

| Entity | Key data |
| --- | --- |
| Game | UUID, editable title, description, favorite, played/status, created/updated |
| LibraryRoot | UUID, canonical path, recursive flag, last completed scan |
| SourceArchive | UUID, game FK, path, size, mtime, optional deferred hash, format, availability |
| Installation | UUID, game FK, directory, optional source archive FK, extraction provenance, availability |
| LaunchProfile | UUID, installation FK, selected executable, working directory, explicit argument list |
| Tag / GameTag | Normalized tag name and many-to-many association |
| Media | UUID, game FK, local relative asset path, type, caption, order |
| PlaySession | UUID, game FK, start time, optional exit time; launcher handoff tracked as uncertainty |
| ExtractionJob | UUID, source FK, staging/destination, state, progress, limits, failure and recovery metadata |

Do not hash entire archives on every scan. Discover by normalized path and metadata, then hash in a cancellable background job when identity reconciliation is requested. Never infer that matching archive and directory names prove extraction. Let the user confirm associations and preserve provenance for app-managed extraction.

## Milestones and acceptance criteria

### 1. Local vertical slice — implemented

Index archives/folders; retain metadata; explicitly link folders; guarded ZIP extraction; choose and launch EXE; local image; automated checks. Acceptance: rescanning does not lose metadata, repeated extraction does not overwrite files, traversal archives fail without promoted output.

### 2. Archive completeness and extraction jobs — next

Evaluate a maintained managed archive library (such as SharpCompress) against official documentation, current advisories, RAR/7z feature coverage and licenses. Alternatively use a separately installed, user-configured 7-Zip executable. Do not silently download executables or concatenate archive paths into shell commands.

Adapt archive entry streams to the same preflight/path/size policy. If a CLI backend cannot reliably expose and constrain links, types, entries and actual expanded bytes, do not claim equivalence to the ZIP policy; isolate it appropriately or reject those archives. Add encrypted archives, password-in-memory handling, multipart recognition, disk-space checks, throttled progress, time limits, persistent job state and crash recovery. Tests need real RAR/7z fixtures, solid archives, malformed inputs, links, traversal, collisions, cancellation mid-stream and resource exhaustion. Review redistribution licenses before bundling anything.

### 3. Durable library and reconciliation

Introduce repositories and transactional SQLite migrations with JSON import/backup. Represent multiple archives/installations per game. Add move/rename reconciliation, explicit split/merge with undo, remove-entry versus delete-files separation, editable roots and filters. Acceptance: a failed scan never marks an entire inaccessible drive as deleted; interrupted migrations recover; all metadata survives moves and merges.

### 4. Browsing and launch quality

MVVM view models and commands, paginated/virtualized cards, gallery, thumbnail cache, keyboard navigation, status filters, ratings and launch profiles. Support explicit argument lists without command-shell parsing. Add play-session tracking with honest handling of launchers that spawn other processes. Acceptance: browse thousands of entries without blocking the UI; screenshots do not lock source files; selection and unsaved edits survive filtering.

### 5. Release hardening

Validate on .NET 10 and supported Windows versions. Add GUI smoke/accessibility tests, extraction fuzz fixtures, file-race analysis, crash-recovery tests, logs with useful privacy controls, code signing and an installer or portable package. Retain per-user data during updates. Ship neither old runtimes nor private library contents. A self-contained distribution must be rebuilt for runtime security fixes.

## Decisions intentionally postponed

Automatic online descriptions/covers require a provider, licensing review and user confirmation for uncertain matches. User-defined tags and descriptions work offline now. Cloud sync, automatic executable choice, auto-launch after extraction, destructive cleanup and elevated execution are outside the initial scope.
