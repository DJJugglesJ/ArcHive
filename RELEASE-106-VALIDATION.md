# ArcHive 1.0.6 validation

- 821 automated checks pass, including half-star validation, review bounds, timestamps, rating sorting/filtering, SQLite round trips, legacy defaults, rescan preservation, provider apply/undo, and retained duplicate-source reviews.
- Release build: zero warnings and errors.
- Live NuGet direct/transitive vulnerability audit: no known vulnerable packages reported on 2026-11-22. This is an advisory-based dependency check, not a comprehensive security assessment.
- SharpCompress updated from 0.50.9 to 0.50.11, including the upstream LZMA2 retained-history limit fix: https://github.com/adamhathcock/sharpcompress/releases/tag/0.50.11
- Microsoft.Data.Sqlite updated from 10.0.14 to 10.0.15. The self-contained release uses the installed .NET 10 servicing runtime; packaged version is recorded in release verification.
- Restore now audits transitive dependencies and fails on audit outages or reported vulnerabilities (NU1900 through NU1904).
- Existing personal library rows, settings and artwork compared unchanged; SQLite integrity and foreign-key checks pass.

Windows was locked during the attempted focused UI check. No ratings/reviews visual or physical-controller acceptance is claimed. Package extraction, payload comparisons, approved icon checks and updater discovery results are recorded in the release verification attachment. Administrative MSI extraction does not constitute an in-place upgrade. Packages are unsigned.
