# ArcHive continuation

## Preservation

The existing project is at GameShelf/ with App, Core and Tests projects. It is a C#/.NET 10 WPF application, not an Avalonia skeleton. At the first continuation this directory had no Git repository. The user subsequently supplied DJJugglesJ/ArcHive; publication preserves its initial commit. A pre-change source/docs/fixture snapshot is at ../work/before-archive-branding (bin, obj and .vs omitted). Existing source directory names, namespaces and assembly names intentionally remain compatible.

Default data remains %LOCALAPPDATA%\GameShelf; branding does not move or reset a user's library. ARCHIVE_DATA_DIRECTORY can point to an isolated data folder for development/testing. No real user database was edited during UI verification.

## Architecture decisions

Keep the working WPF UI for this increment, as allowed by the approved preference for Avalonia unless the existing project dictates otherwise. Keep scanner/extraction/storage in Core. Provider integrations and new persistent fields require tested SQLite migrations; do not add UI-only fields that silently disappear on restart.

Future provider results should carry provider ID, external ID, candidate confidence/evidence and field-level values with retrieval timestamps. A separate reconciliation service preserves user overrides and returns a reviewable patch. Provider responses must not directly mutate Game. F95Zone thread associations and optional launch adapters belong behind the same boundaries, not in scanner code.

## Current deliverables

ROADMAP.md is the current approved plan and status ledger. design/DESIGN.md and design/tokens.json capture identity, palette, implementation and approved original branding. The app has ArcHive naming, dark artwork browsing, archive navigation and conservative filename cleanup. Historical ARCHITECTURE.md and validation/migration records are preserved.
