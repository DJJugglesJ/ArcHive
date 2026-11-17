# ArcHive 1.0.3 verification

- Automated suite: 769 checks passed. Release build: zero warnings/errors.
- Tag regressions cover separator normalization, duplicate removal, exact case-insensitive filtering and F95 header links without reply-tag contamination.
- Automatic launch regressions cover version/platform/CamelCase normalization, helper exclusion, ambiguous and unrelated executables, and preservation of custom profiles/arguments.
- Essential isolated desktop check: matching executable receives an Automatic profile, Play launches a harmless fixture, tags can be edited and deduplicated, and a clicked tag opens a filtered collection with a clear-filter action. Profile and tag changes persist. SQLite integrity and foreign-key checks pass.
- Layout correction: library tag browsing is hidden in details; game tag chips and editing/import actions sit alongside the primary game actions. Automatic setup refreshes the displayed installation status.
- F95 parsing is verified with fixtures; no new authenticated live F95 lookup was performed. Public-page access and provider availability still apply. F95 tools must be enabled in Settings; imported fields remain subject to review.
- Automatic launch applies only to untouched profiles and one matching non-helper executable at the installation root. Nested, ambiguous and differently named candidates still require Launch Options. It chooses a profile; it never runs a game without a Play action.
- Controller diagnostics used the real routing path; no physical gamepad was tested. No broad end-to-end UI sweep was performed.
- Package and publication results are recorded in the release verification attachment.
