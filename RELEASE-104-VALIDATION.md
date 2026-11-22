# ArcHive 1.0.4 validation

- 787 automated checks passed, including the download-review-preview-extract-persist pipeline, unsafe URL/name rejection, modified files, queue locking, restart recovery and archive lineage.
- Release build passed with zero warnings and errors.
- Focused live UI: an isolated queue downloaded the public octocat/Hello-World ZIP through HTTPS redirects; reviewed its hash; previewed its two entries; queued and completed extraction; persisted one installation. Extracted README contents matched. SQLite integrity and foreign-key checks passed.
- Fixed excessive progress callbacks and overlapping/uncancellable archive verification. No broad UI sweep or physical-controller acceptance test was performed.
- Public permanent HTTPS ZIP/RAR/7z links only; authenticated pages, signed query links and checkout use the browser. No automatic extraction or launch.
- Package and publication checks are recorded in the release verification attachment. Existing personal library and installed app were not modified by the isolated UI test.
