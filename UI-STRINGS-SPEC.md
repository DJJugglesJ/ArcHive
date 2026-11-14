# ArcHive — Consolidated UI String Implementation Specification

(Terminology finalized)

**Repository\:** `DJJugglesJ/ArcHive`
**Reference branch\:** `main`
**Scope\:** Application-wide user-facing text, terminology, messages, accessibility labels, and navigation
**Implementation status\:** Specification only — no source changes

This specification consolidates the approved wording decisions and the recommendations from our source review. It distinguishes straightforward text replacements from changes requiring layout, behavior, or compatibility considerations.

## 1\. Approved terminology dictionary

These terms are authoritative for the implementation.

| Concept | Final wording |
|--------|--------|
| Main library | **Library** |
| Archive navigation | **Archives** |
| Library organization | **Organize Library** |
| Adult content | **NSFW** |
| Game progress | **Play Status** |
| Game availability | **Installation Status** |
| Online game lookup | **Find Game Information** |
| Archive workflow | **Manage Archive…** |
| Executable/engine detection | **Scan Game Files** |
| File location action | **Open Game Folder** |
| Save management | **Save Backups** |
| Empty library title | **Your library is empty.** |
| Empty library description | **Add a library folder to discover your games and archives.** |
| Tagline | **From Archive to Adventure.** |
| Developer attribution | **Bellum Labs · Nicholas Gartin** |

**Metadata** remains the technical term for imported or provider-supplied game information. **Snapshot** remains the term for a specific saved backup instance.

### Writing conventions

- Use Title Case for buttons, tabs, navigation, and field labels.
- Use sentence case for descriptions, instructions, status messages, and errors.
- Use concise verb-first button labels: `Add`, `Scan`, `Save`, `Restore`, `Remove`\.
- Use an ellipsis (`…`\) when an action opens a dialog or requires additional user input.
- Avoid referring to a game as an *entry* unless discussing an internal record.
- Preserve technical details when they explain a restriction, failure, or safety concern.
- Use consistent American English, including `Canceled` and `Canceling`\.

---

## 2\. Navigation and primary window

Files: `MainWindow.xaml`, `MainWindow.xaml.cs`, `MainWindow.Gamepad.cs`, `MainWindow.DetailPage.cs`

| Existing | Final replacement |
|--------|--------|
| `YOUR GAMES. PRESERVED.` | `FROM ARCHIVE TO ADVENTURE.` |
| `COLLECTION` | `LIBRARY` |
| `All games` | `Library` |
| `Archive manager` | `Archives` |
| `MANAGE` | `TOOLS` |
| `Organize library` | `Organize Library` |
| `Your library` | `Library` |
| `Your favorites` | `Favorites` |
| `A home for every game you discover.` | `Your games, all in one place.` |
| `← Back to collection` | `← Back to Library` |
| `Cancel operation` | `Cancel` |

Retain `Favorites`, `Activity`, `Settings`, and `Add Games`\.

The existing `Activity` navigation item currently opens extraction activity. Until it includes other operations, the preferred descriptive text should identify it as extraction-related. Retain the short label `Activity` for navigation, but title the window `Extraction Queue`\.

### Library empty states

| Condition | Heading | Supporting text |
|--------|--------|--------|
| No indexed games | Your library is empty. | Add a library folder to discover your games and archives. |
| Search returns no matches | No matching games | Try changing your search or filters. |
| Game not selected | Select a game | Select a game to view its details. |
| Missing description | No description available. | Use Find Game Information or add your own description. |

Replace the existing library count presentation with\:

`Showing {visible} of {total} games · {folders} library folders`

Preserve the actual numbers dynamically.

---

## 3\. Game details layout

The approved tab order is\:

File: `MainWindow.xaml` and associated partial classes

| Tab | Contents |
|--------|--------|
| **Overview** | Description, title, Favorites, Played, NSFW, controller support, Play Status, tags, and general details |
| **Media** | Covers, backdrops, screenshots, artwork sources, and media management |
| **Versions** | Known version, platform, channel, release history, update status, version provenance |
| **Launch** | Installation selection, executables, launch profiles, arguments, scanning, linked folders, and archives |
| **Sources** | Game information providers, linked external references, F95Zone thread and version information |
| **Backups** | Save folders, backup management, and restore actions |

Preserve all existing functionality when restructuring the tabs.

### Overview strings

| Existing | Replacement |
|--------|--------|
| `About this game` | `Description` |
| `Edit game details` | `Edit Details` |
| `Controller input support` | `Controller Support` |
| `Progress` | `Play Status` |
| `Tags (comma separated)` | `Tags` |
| `Description / notes` | `Description & Notes` |
| `Find metadata` | `Find Game Information` |
| `Undo metadata` | `Undo Information Changes` |
| `Release year / engine` | `Release Year` and `Game Engine` |
| `Save details` | `Save Changes` |

The `Played` checkbox and Play Status field should retain their existing meanings. Their relationship should be verified during implementation to avoid contradictory display states.

### Play Status

Maintain the existing persisted enum values and their current behavior.

| Value | Display |
|--------|--------|
| `Backlog` | Backlog |
| `Playing` | Playing |
| `Completed` | Completed |
| `Abandoned` | Abandoned |

### Installation Status

| Existing state | Display |
|--------|--------|
| `Ready` | Ready to Play |
| `Folder linked` | Folder Linked |
| `Archived` | Archived |
| `Not installed` | Not Installed |
| `Missing` | Missing Files |

The label **Installation Status** applies to the complete availability indicator, including games that are archived or not installed. On compact cards, `Ready` may remain abbreviated.

Do not change serialized state values simply to change what the UI displays.

---

## 4\. Media, versions, and launch

### Media

| Existing | Replacement |
|--------|--------|
| `Artwork` | `Media` |
| `Clear cover` | `Clear Cover` |
| `Clear backdrop` | `Clear Backdrop` |
| `Remove screenshot` | `Remove Screenshot` |
| `Add artwork` | `Add Artwork…` |

Proposed explanation\:

> Manage covers, backdrops, and screenshots. Use Find Game Information to discover additional artwork.

### Versions

| Existing | Replacement |
|--------|--------|
| `Latest known version (manual)` | `Latest Known Version` |
| `Latest platform / channel` | `Latest Release Details` |
| `Latest source / note` | `Version Source / Notes` |
| `Observed release history` | `Release History` |
| `No latest release recorded` | `No known release recorded` |
| `Latest recorded` | `Last Recorded` |

Retain `Update Available`, `Up to Date`, and `Unknown`\.

Version comparison must continue to distinguish locally recorded releases from online-verified information.

### Launch

| Existing | Replacement |
|--------|--------|
| `Files & launch` | `Launch` |
| `Detect launch options` | `Scan Game Files` |
| `Open source folder` | `Open Game Folder` |
| `Folder to launch from` | `Installation` |
| `Launch profile` | `Launch Profile` |
| `New profile` | `Add Profile` |
| `Remove profile` | `Remove Profile` |
| `Make default` | `Set as Default` |
| `Profile name` | `Profile Name` |
| `Executable (inside installation)` | `Executable` |
| `Arguments (one row per argument)` | `Launch Arguments` |
| `Working directory (blank = executable folder)` | `Working Directory` |
| `Choose executable` | `Choose Executable…` |
| `Save details and profiles` | `Save Changes` |

**Behavior requirement\:** The approved `Open Game Folder` action should open the selected installation or extracted game directory. When only an archive is available, the action should either open its containing folder with an accurate explanatory tooltip or be disabled until a game folder exists. It must not misleadingly present an archive directory as an installation.

---

## 5\. Archives and extraction

Files: `ArchiveQueueWindow.xaml`, `ArchiveQueueWindow.xaml.cs`, `ExtractionQueue.cs`, `ArchiveInspection.cs`, `MultiArchiveExtractor.cs`

| Existing | Replacement |
|--------|--------|
| `Archives and extraction queue` | `Extraction Queue` |
| `Preview / queue` | `Manage Archive…` |
| `Preview contents` | `Preview Archive` |
| `Add to queue…` | `Queue Extraction…` |
| `Run pending / finish linking` | `Process Queue` |
| `Retry selected` | `Retry Selected` |
| `Cancel pending entry` | `Cancel Queued Job` |
| `Destination parent` | `Destination Folder` |
| `Result / error` | `Result` |

Use `Cancel Queued Job` rather than `Remove from Queue` because the current operation changes a pending job's state to canceled rather than necessarily deleting its history.

### Status messages

| Event | Final text |
|--------|--------|
| Archive queued | Added to queue. Start extraction when ready. |
| Preview completed | Archive preview ready. File integrity will be checked during extraction. |
| Unsupported entries | Some archive entries aren't supported. Review the Issue column. |
| Queue stopped | Extraction stopped. Remaining jobs are still queued. |
| Archive changed | The archive has changed since it was queued. Preview it again before extracting. |
| Missing output | The extracted folder can't be found. Restore it before linking. |
| Interrupted job | Extraction was interrupted. Retry to start again. Previous files were retained. |

Retain all safety checks and extraction restrictions.

---

## 6\. Sources and game information

Files: `MetadataWindow.xaml`, `SourceConnections.xaml`, `F95ThreadWindow.xaml` and associated code-behind files

| Existing | Replacement |
|--------|--------|
| `Find the right game` | `Find Game Information` |
| `Search selected provider` | `Search Provider` |
| `Provider settings` | `Manage Connections` |
| `Import metadata JSON` | `Import Metadata from JSON` |
| `Apply selected changes` | `Apply Changes` |
| `Your sources, connected clearly` | `Manage Your Sources` |
| `Open website / sign in` | `Open Provider Website` |
| `Remember securely in Windows` | `Save Credentials Securely` |
| `Disconnect this source` | `Disconnect` |
| `A source you can trace` | `Linked F95Zone Thread` |
| `Check public thread` | `Check Thread` |
| `Use observed version as latest known version` | `Use Thread Version as Latest` |

Retain service names as their actual names: Steam, GOG, itch.io, RAWG, IGDB, VNDB, and F95Zone.

Keep provider credentials and authentication explanations technically accurate.

### Recommended messages

**No matches\:**

> No matches found. Try another search term or provider.

**Lookup failed\:**

> Couldn't retrieve game information. No changes were made.

**Matching notice\:**

> Matches are suggestions, not verified identities. Review each result and choose which fields to update.

**Credentials rejected\:**

> Access was denied. Check your credentials and permissions in Sources.

---

## 7\. Organize Library

Files: `OrganizationWindow.xaml`, `OrganizationWindow.xaml.cs`

| Existing | Replacement |
|--------|--------|
| `Organize your library` | `Organize Library` |
| `Select all shown` | `Select All Visible` |
| `Clear selection` | `Clear Selection` |
| `Compare archive contents (SHA-256)` | `Compare Archive Contents` |
| `Duplicate evidence` | `Potential Duplicates` |
| `Set progress status` | `Set Play Status` |
| `Add to favorites` | `Add to Favorites` |
| `Remove from favorites` | `Remove from Favorites` |
| `Clear NSFW mark` | `Remove NSFW Tag` |
| `Remove library entries` | `Remove from Library` |
| `Preview changes` | `Preview Changes` |
| `Apply preview` | `Apply Changes` |

The `Remove NSFW Tag` wording is user-facing only; this still changes the existing NSFW classification rather than adding a literal game tag.

### Destructive action confirmation

> Remove  games from your library? Their saved ArcHive information will be removed, but original game folders and archives will remain on disk.

Buttons: **Remove from Library** and **Cancel**\.

---

## 8\. Save Backups

Files: `SaveBackupsWindow.xaml`, `SaveBackupsWindow.xaml.cs`, `ArcHive.Core/SaveBackups.cs`

| Existing | Replacement |
|--------|--------|
| `Save backups and restore` | `Save Backups` |
| `Keep regular snapshots:` | `Snapshots to Keep` |
| `Back up now` | `Create Backup` |
| `Preview restore` | `Preview Restore` |
| `Restore snapshot` | `Restore Snapshot` |
| `Recover interrupted restore` | `Recover Restore` |
| `Backup completed:` | `Save backup created:` |
| `Restore completed.` | `Save files restored successfully.` |

The snapshot retention label must clarify that automatic pruning applies only to regular snapshots, not protected pre-restore snapshots.

Keep the confirmation warning\:

> Restoring replaces the entire save folder. Close the game and pause cloud syncing before continuing. Current saves will be backed up first.

---

## 9\. Settings, controller help, and About

### Settings

| Section | Final heading |
|--------|--------|
| Library | Library Preferences |
| Sources | Source Connections |
| Controls | Input &amp; Navigation |
| Storage | Storage &amp; Backups |
| Optional Sources | Optional Sources |
| About | About ArcHive |

Retain the current sections until there is an explicit decision to merge them.

Controller help should be maintained in one shared definition so the settings text, application menu, and controller help dialog stay synchronized.

### About ArcHive

# ArcHive

From Archive to Adventure.

Version {version}

Bellum Labs · Nicholas Gartin

© {year} Bellum Labs. All rights reserved.

The version should come from the application assembly, not a hardcoded string. The copyright year should use the project's configured copyright information; the specification does not require changing its existing year policy.

---

## 10\. Error and status message standards

Rather than replacing all exception text with generic messages, use the following patterns.

| Situation | Standard |
|--------|--------|
| Successful operation | `{Action} completed.` or a specific result |
| Missing required selection | `Select a {thing} first.` |
| Missing file/folder | `{Thing} couldn't be found. {Recovery action}.` |
| Invalid input | Explain the exact restriction |
| Failed provider request | Identify the provider when known and suggest corrective action |
| Canceled operation | State whether changes were made |
| Destructive operation | Describe precisely what will be changed or retained |
| Corrupt internal data | User-readable error with expandable technical details |

Do not swallow detailed errors needed for troubleshooting. Preserve the original exception text in diagnostic details.

---

## 11\. Implementation classification

**Text-only changes**

Navigation labels, button captions, section headings, short descriptions, tooltips, and most fixed status messages.

**Shared text definitions**

Common actions, controller instructions, status labels, provider terminology, and repeated prompts. Prefer centralized resources or display mappings over duplicate literals.

**UI structure changes**

Six-tab game details organization, dedicated field labels, status categorization, and potentially the placement of backup controls.

**Behavior and compatibility verification**

`Open Game Folder`, primary action behavior, enum display mappings, archived-job handling, existing metadata undo behavior, and obsolete ZIP-only extraction messages.

## 12\. Acceptance criteria

Implementation should be considered complete when\:

- Every approved term is used consistently across visible navigation, dialogs, context menus, status text, and accessibility names.
- Existing feature behavior remains intact except for explicitly approved UI adjustments.
- The six-tab game details layout contains all existing controls and workflows.
- Play Status, Installation Status, and Update Status are distinct.
- Serialized enum names, saved library data, and database compatibility are preserved.
- All original-file retention and save-restore safeguards remain accurate.
- Primary action labels and accessibility names describe the action actually performed.
- No obsolete or contradictory wording remains in reachable runtime paths.
- Automated tests are updated where they depend on presentation strings, and the application builds successfully.

---

## Specification status

**Approved terminology consolidated**

**UI and runtime wording requirements documented**

**Implementation boundaries established**

**Code modifications: None**

This is the implementation specification for the agreed wording, grounded in the source review. It is not yet an exhaustive machine-readable inventory of every string occurrence or a verified build-level change plan.

The intended implementation is a coordinated **UI terminology and wording pass**, with only the structural and behavioral adjustments required to make those labels accurate.