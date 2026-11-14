# Archive previews, extraction queue and recovery (0.8.0)

## Preview and enqueue

Select an archive in **Files and launch setup**, then choose **Preview / queue**. **Preview contents** reads the archive directory off the UI thread and shows entry paths, directory flags, declared sizes, archive bytes and declared expanded bytes. The table virtualizes rows. ZIP, RAR and 7z use the existing .NET/SharpCompress dependencies.

Preview checks path syntax, reserved names, duplicate paths, file/directory conflicts, size/entry limits and exposed link/special/encryption metadata. Unsupported entries have an Issue and prevent enqueueing from this preview. The default policy remains 100,000 entries and 20 GiB expanded output. Preview is not payload verification: checksum failures, some ZIP encryption and decoder errors may only be discovered during extraction. Encrypted headers can make the preview itself unavailable. Extraction always performs its own validation.

Choose **Add to queue**, select an extraction parent, and either run now or close the dialog to add another archive. Open **Extraction queue** from the toolbar to review all persisted jobs. Each job captures its game/archive IDs, release metadata, source length/write time, chosen parent and a stable installation ID. Enqueueing creates no extraction output. Duplicate active jobs for the same game/archive path/destination are rejected.

## Running and stopping

**Run pending / finish linking** processes a snapshot of Pending and Extracted jobs sequentially. No jobs start automatically on app launch. The source fingerprint is checked, a read handle prevents ordinary concurrent source writes/deletion, and preflight runs again. Every attempt has a fresh `ArcHive-extract-<job-id>-<attempt-id>` container beneath the chosen parent. The existing extractor stages and publishes its game directory inside that container without overwriting older output. Original archives are retained.

**Stop** cancels the current operation and leaves later jobs Pending. Cancellation is cooperative: a decoder read must return before cancellation can be observed. Individual failures record an error and allow the next job to run. Select a pending job and choose **Cancel pending entry** to skip it. **Retry selected** requeues Failed, Cancelled or Interrupted work; it never reuses partial output. If the archive changed, review and enqueue a new job rather than retrying the old fingerprint.

The dialog cannot close during work: a close request cancels and asks you to close again once stopped. The queue runs while the dialog is open, not as an unattended background service. Extracted installations still require explicit executable/profile selection before launch.

## Recovery states

| State | Meaning and next action |
| --- | --- |
| Pending | Persisted, not started. Run when ready. |
| Running | A durable attempt record exists before output creation. A restart changes this to Interrupted. |
| Interrupted | No durable completion receipt. Inspect retained attempt paths; Retry starts fresh. |
| Extracted | Output path was durably recorded; library linking is unfinished or its acknowledgment was interrupted. Run again to finish linking without extracting. |
| Completed | Library linking succeeded and queue completion was recorded. No further work runs. |
| Failed | Inspect the error and correct the cause before Retry. |
| Cancelled | Explicitly stopped/skipped. Retry is available. |

The queue journal is `%LOCALAPPDATA%\ArcHive\extraction-queue.json` (or under `ARCHIVE_DATA_DIRECTORY`). Writes flush a temporary file and atomically replace the journal, retaining a rolling `.bak`. A `.lock` handle prevents concurrent queue owners in the same data directory. An old `.tmp` is not treated as committed state.

After extraction returns, the queue records its output as Extracted **before** saving the library. Library linking uses the fixed installation ID and checks its archive association. If ArcHive stops after the library save but before recording Completed, replay finds that installation instead of adding another. If library saving fails, output remains Extracted and retry only repeats linking. Removed games/archives are not recreated silently; the output remains available for manual linking.

A stop after publication but before the output receipt is deliberately treated as Interrupted. ArcHive cannot prove that an unrecorded folder is complete, so it does not adopt it automatically. Select a job to see its archive, output, error and all attempt paths. Partial staging and uncertain output from a crash are retained; recovery never recursively deletes them. Ordinary extractor errors/cancellation still use the existing extractor's staging cleanup. Empty attempt containers may remain.

If a recorded Extracted output is missing, restore its original path before retrying linking, or manually recover the files. Recovery is not an in-place decoder resume, automatic orphan cleanup or a cross-database transaction. Source length/write time detects ordinary changes but is not a cryptographic identity. Use user-controlled local destinations without hostile filesystem races.

## Damaged journals and backups

Unsupported or corrupt journals fail closed and remain untouched; the app does not silently replace them with an empty queue. Close all ArcHive instances and retain copies of the journal, `.bak`, library database and output folders before attempting recovery. Inspect the backup as well as current state: an older backup can lack a newer completion receipt. Never edit attempt/output paths to point at unrelated directories. The library recovery procedure is in [SQLITE-MIGRATION.md](SQLITE-MIGRATION.md). Completed jobs remain in the journal; automatic history pruning is not implemented (10,000-job / 20 MiB journal limits).

## Verification

469 automated checks pass, including 39 new archive-workflow checks. Coverage includes ZIP/RAR/7z previews, dangerous paths and limits, preview cancellation, persistent queue reload, exclusive journal ownership, atomic enqueue failure, source mutation protection, source-change rejection, sequential continuation, cancellation, retry isolation, retained partial staging, output receipt replay, failed linking and idempotent linking after a database-save boundary. Tests restore actual durable journal snapshots at interrupted stages; they do not simulate hardware power loss or abrupt OS termination. Full end-to-end UI tests were skipped. Decoder resource limits and a broader corruption/link-metadata corpus remain hardening work.
