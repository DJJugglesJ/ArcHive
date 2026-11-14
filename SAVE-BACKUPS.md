# Save backups and restore

Available in source version 0.10.0 through game details → Save backups, or right-click / Triangle options → Save backups and restore.

## Workflow

1. Add a game-specific **save folder** explicitly. Multiple folders can be linked per game, and are backed up/restored separately. No save locations are guessed.
2. Close the game and pause any cloud-save syncing. Select the folder and choose **Back up now**.
3. Select a timestamped snapshot and choose **Preview restore**. The preview names every added, replaced or removed entry and the full destination.
4. Confirm **Restore snapshot**. Restore replaces the whole folder, including removal of files absent from the snapshot. A changed preview is rejected and must be generated again.
5. To undo, select the protected **Before restore** snapshot and preview/restore it normally.

Save locations and retention settings persist in the SQLite game metadata. Existing libraries start with no save locations. Normal rescans and batch edits preserve this configuration. Removing a game from the library does not delete its on-disk snapshots.

## Storage and retention

Snapshots live at `%LOCALAPPDATA%/ArcHive/save-backups/<game-id>/<location-id>/<snapshot-id>/`, or under `ARCHIVE_DATA_DIRECTORY` when set. Each contains `snapshot.json` and a `files` directory. They are local, uncompressed and unencrypted; this is not a substitute for an independent disk or cloud backup.

Keep 1–100 regular snapshots per location (default 10). Saving retention settings takes effect after the next successful backup. Only complete, verified regular snapshots are pruned. Protected pre-restore snapshots are never automatically deleted.

Restore also retains the displaced original folder beside the save folder as `.archive-previous-<transaction-id>`. This intentionally uses extra space. Keep it until you have checked the restored saves; remove it manually only when no longer needed. Failed/cancelled staging folders (`.partial-*` in backup storage or `.archive-restore-*` beside saves) are excluded from completed snapshots and may also need manual cleanup after confirming no operation is running.

## Safety and recovery

- Ordinary file contents, relative layout, empty folders and file modification timestamps are copied. ACLs, alternate data streams and other filesystem-specific metadata are not backed up.
- SHA-256 content checks verify the source before/after backup, the stored snapshot, and the staged restore. Hashes detect accidental corruption; they are not an authenticity guarantee against someone editing both data and manifest.
- Defaults bound each snapshot to 2 GiB of file contents, 10,000 entries and 64 path components. Links/reparse points, drive roots, broad user-data roots, and overlapping save/backup folders are rejected.
- An exclusive per-location operation lock prevents simultaneous operations using that snapshot store. The game and cloud clients do not participate in this lock: close/pause them first. This is not a filesystem-level snapshot of a running game.
- Work runs off the UI thread. Cancellation is checked during scanning/copying and before the final swap; once journaled replacement begins it finishes without cancellation. Closing a busy window requests cancellation and keeps it open until safe.
- Before replacement, a protected snapshot of existing saves is created; failures leave the originals untouched. A restore into a missing save folder needs its parent directory to exist.
- A durable intent record precedes same-parent directory moves. If interrupted, further backup/restore operations stop until **Recover interrupted restore** is used. When the destination is absent, recovery moves the displaced original back. When it is present, recovery leaves it and any extra copies intact. Unexpected/mismatched recovery records fail closed.
- Recovery tests simulate process interruption at filesystem boundaries. Hardware power-loss durability and active cloud-sync interference have not been validated.

## Validation

40 focused automated checks cover snapshot contents, restore/undo, retention, corruption, stale state, cancellation, bounds, locked saves, missing payloads, link rejection, manifest traversal, recovery and SQLite persistence. Together with the existing suite: 551 passing checks. Full end-to-end UI tests were skipped as requested.
