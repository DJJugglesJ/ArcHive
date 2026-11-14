# ArcHive 1.0 workflows

## Playtime

Launching a game starts a recorded session. Keep ArcHive open while playing. The monitor checks process IDs **and start times**, observes child processes while their parent is visible, and continues until those observed processes exit. It polls every two seconds and saves checkpoints approximately every ten seconds. Time during monitoring gaps longer than 30 seconds is excluded and marked for review. Closing ArcHive or recovering an unfinished session preserves recorded time and requests review; it never terminates the game.

Fast launcher handoffs that happen between polls cannot be reconstructed. Short sessions with no observed child, access failures and interrupted monitoring can need correction. Open **Playtime & Naming…** in Overview to review sessions, replace a completed duration in minutes or add a manual session. The Played flag and Play Status remain independent.

## Merge duplicates

Open **Organize Library**, select games, then **Batch Tools… → Preview Duplicate Merge**. Choose the primary game. Its title, description, metadata provenance, artwork, status and existing defaults win conflicts. All archives, installations, profiles and association IDs remain; identical paths can retain separate configurations. Sessions and collections are combined; favorites, Played and NSFW flags are retained when set on either game.

Other complete records remain visible in **Playtime & Naming… → Preserved Merge Records**. Existing save folders retain their original backup-owner ID so old snapshots can still be listed and restored. Original files are not deleted. Merge after extraction jobs and tracked play sessions finish. A stale library invalidates the merge preview.

## Batch tools

- The main Organize action list supports favorites, Play Status, tags, NSFW, Played and collections, plus record-only removal. Review before/after values, then apply in one database transaction.
- Batch extraction previews each selected archive before queueing the set atomically. Run the jobs from **Extraction Queue**. Existing safe extraction and recovery behavior applies.
- **Scan Selected Game Files…** walks the selected installed games through the evidence-review dialog. Each accepted result contributes to a final preview; the database changes only after that preview is applied. Existing manual launch defaults remain intact.
- Rename uses the reviewed canonical title, falling back to the display title. Relocation keeps filenames. Both operate on the selected games' registered archives and installation folders and rebase linked executable/working-directory paths.

File changes require an existing destination on the same drive. Existing targets, reserved/invalid names, shared or nested source paths, links, library roots and folders containing configured saves are rejected. Close affected games and tools first. Arguments and external plugin configurations are not rewritten; review any absolute paths in them before moving files.

File previews hash contents and directories, with a 50,000-entry limit. Apply rechecks content, writes and flushes `file-operations.json`, moves without overwriting, verifies destinations, then saves the library. Uncommitted moves roll back on recovery; committed moves are verified and retained. Startup handles a pending journal before normal use. Changed or ambiguous files stop recovery without deleting or overwriting them. Keep the journal and its paths for manual review. Completed/recovered journals remain as evidence.

## Naming and browsing

Naming stores original name, reviewed canonical title, display title, parser confidence and evidence separately from location version/platform/channel. Existing manual display titles survive review unless **Use Canonical Title** is selected. Bare numbers and date-like tokens are retained, with ambiguous dates given low confidence. No naming review changes files automatically.

**Browse…** combines collection, exact tag, engine, installation availability, controller support and unplayed filters. Choose title, recent-play or playtime ordering, or retain Library Settings order. These filters intersect with search, favorites, Play Status and NSFW visibility. Search also covers canonical/original names and collections. Statistics respect NSFW visibility. Collections are assigned through Organize's previewed batch tools.

## Start fresh

**Settings → Storage & Backups → Purge Library…** requires explicit confirmation. It clears game records, registered roots and extraction history, with a permanent pre-purge database copy and queue backup. Games, archives, saves, snapshots, artwork, preferences, credentials and installed plugins stay on disk. Running operations and tracked play sessions must finish first. A durable marker lets startup finish queue cleanup after an interruption.

## Compatibility and release checks

SQLite remains schema 2; new game fields are additive JSON metadata. Existing enums and IDs remain unchanged. Keep a database backup before opening a 1.0 library in older ArcHive versions: older writers do not preserve new fields. No full end-to-end UI test was run for this milestone. Automated coverage and build results are recorded in VALIDATION.md.
