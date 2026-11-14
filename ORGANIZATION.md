# Duplicate review and batch organization (0.7.0)

Open **Organize** from the library toolbar. The organizer captures the current visible list, respecting search, progress, favorites, archive view and the NSFW visibility preference. Ctrl/Shift multi-select and Select all shown operate on this scope only. A separate modal window keeps the main inspector and gamepad navigation from modifying the selection during review.

## Duplicate evidence

Quick review groups normalized absolute Windows paths and parsed titles. Path groups can include multiple associations within one game and unavailable locations; they mean shared references, not verified files. Possible-title groups remove the parser's explicit release/platform tokens but are never called exact duplicates. Empty titles are ignored. No transitive group merging combines weak and strong evidence.

Optional SHA-256 comparison runs off the UI thread. Each distinct archive path is considered once, with size as a prefilter; unique-size archives need no read. Same-size candidates are streamed through SHA-256 with bounded buffers and cancellation. Open handles deny concurrent writing/deletion, and size/write-time changes detected around reading are skipped. Missing, locked, linked and otherwise unreadable files produce warnings instead of false matches. Paths are checked for reparse points. This is a local snapshot, not protection against hostile filesystem races or a promise that files cannot change later. Hashes are not persisted and a later scan recomputes them.

Matching hashes across distinct paths produce ExactArchive evidence. Hashing compressed files compares archive bytes, not extracted contents: differently packed copies of the same game may not match. Installations and executables are not content-hashed; common engine executables are not reliable whole-game identity. Different physical paths may be hard links to the same data; no reclaimed-disk-space estimate is made.

Select a group to see its exact evidence and member paths; this selects its associated game entries. Review the rows before applying any action. Multiple archive copies within one entry do not create independently removable archive rows. This milestone does not merge game records, deduplicate location associations, rename/move files, delete files or automatically choose a winner.

## Batch actions

- Add/remove favorites.
- Set Backlog, Playing, Completed or Abandoned independently of played history.
- Add/remove whole comma-separated tags, trimming whitespace and deduplicating case-insensitively. Existing spelling/order is retained; changed tag fields receive manual provenance.
- Mark/clear NSFW classification. Applying a hidden classification removes the entry from a hidden-NSFW library view after refresh.
- Remove library entries and their stored metadata/associations; preserve original archive and installation files and scan roots. A later rescan can rediscover removed paths.

Preview lists every selected entry with before/after values, including no-op changes. Selection, action, tags and status changes invalidate the preview. Removing entries requires a second explicit confirmation, defaulting to No.

The core validates the complete selection and request before building a deep-copy plan. IDs, paths, launch profiles, release history, artwork and unrelated metadata are preserved. Apply saves the whole result in one SQLite transaction and replaces the live library only on success. A save failure retains the current library; no partial batch is published. The existing rolling `.bak` database contains the pre-save state and is overwritten by later saves. There is no persistent batch undo/history journal; use the documented database recovery procedure when appropriate.

## Verification

49 new checks bring the suite to 430. They cover strong vs weak duplicate evidence, equal-size different bytes, shared references, title versions, empty scopes/titles, missing/invalid/locked/changed paths, cancellation, supplied visibility scope, all batch actions, invalid/stale selections, tag semantics/provenance, source immutability, launch-profile preservation, database persistence/backup and transaction rollback. Eight component checks construct the actual organizer without showing a window to verify selection/argument preview invalidation and save-error behavior. Full end-to-end UI automation was not run.

Batch extraction, identification, relocation, merge/reconciliation and a recovery journal remain separate roadmap work.
