# SQLite schema 2 migration

ArcHive 0.2.0 supports schemas 0 (new), 1 and 2. Before upgrading schema 1 it creates a separate online SQLite backup named `library.db.pre-v2-<unique-id>.db`. New columns and the launch_profiles table, transformed metadata, and the schema version are committed in one transaction. A migration failure rolls all schema/data changes back. Unsupported future schemas fail closed.

Archives and installations store independent release metadata. Launch profiles are owned by installation, with a persisted default validated against that owner. Progress, latest-release provenance and release observations live in game metadata. Existing titles, IDs, favorites, notes, tags, images, played flags, last-launch time, roots and location links survive migration. Existing executable choices become Default profiles. Legacy played games become Playing and unplayed games Backlog, unless an explicit status already exists.

Schema-2 loads never recreate deleted profiles from legacy executable fields. Rescans preserve user edits. The usual `library.db.bak` remains an online snapshot before each save; the permanent pre-v2 backup is not overwritten by later saves. Save failures roll back the complete transaction.

To roll back to a pre-0.2 binary, close ArcHive, preserve the current data folder, and restore the pre-v2 database as `library.db`. Do not use a schema-2 `.bak` with an older binary. Artwork and game files are not modified by migration. No user library was migrated during this milestone's verification; tests use disposable databases.

## Earlier JSON import and recovery notes

## SQLite and multiple locations

On first launch the app creates `%LOCALAPPDATA%\ArcHive\library.db`. Before import it parses the old library.json and writes a unique `library.json.pre-sqlite-*.bak` copy. The original JSON and screenshot assets remain untouched. Schema creation and import commit in one SQLite transaction. Existing SQLite databases are authoritative: subsequent starts do not reimport JSON. Unsupported schema versions fail closed.

Every save is transactional. `library.db.bak` is an SQLite online backup of the previous committed state. Close the app before manually restoring that file. Retain the entire ArcHive data folder, including screenshots and legacy JSON. Do not run the old JSON-based app to edit the same library after migration.

Each game now has archive and installation collections with stable IDs. Installations keep their own executable and optional source-archive ID. Game-level IDs, titles, notes, tag text, favorite/played flags, launch timestamp, cover paths and library roots migrate unchanged. Missing paths are retained rather than dropped.

Use **Add archive** to attach one or more ZIP/RAR/7z files. Use **Add folder** to append an existing installation. Choose **Archive to extract**, then Extract archive; each completed extraction adds another installation. Choose **Folder to launch from**, then Choose executable or Launch game. Existing folders and launch settings are retained. Repeated attachments of the same path to one game are ignored. Selectors default to the first location after restarting; selection preference is not persisted.

Attaching a path already listed on another game does not delete or merge that game's metadata. Those cards remain separate until a future explicit merge feature. Scanning recognizes all attached locations. No filesystem content is moved or deleted by linking.

Storage: relational games, archives, installations and roots tables. Game metadata is a versioned JSON payload in the games table; archive/install relationships and provenance use foreign keys. The current repository saves a full snapshot within one transaction and retains the existing single-instance UI policy. It is not a multi-user database or incremental large-library synchronization layer.

Validation: 94 checks passed on .NET 10, including migration backup/source retention, all scalar metadata, multiple paths and executable choices, rescan deduplication, transaction rollback, readable backup, corrupt-source protection and migration retry, plus the existing archive suite.


## 0.3.0 metadata compatibility

Schema remains 2. Provider field provenance, retrieval timestamps and undo history are additive properties in each game's JSON metadata, alongside release year and engine. Absent fields in older records initialize empty. Normal transaction and database-backup behavior is unchanged. Older builds will discard new properties on save; preserve a backup before downgrading.


## 0.5.0 artwork/controller compatibility

Schema remains 2. Independent artwork references and controller-support state are additive game JSON properties. Old games retain their local ScreenshotPath fallback and default to Unknown controller support. Artwork cache files are expendable and separate from library backups; source references remain in the database. Older builds can discard these properties when saving, so retain a backup before downgrading.
