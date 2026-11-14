# SQLite and multiple locations

On first launch the app creates `%LOCALAPPDATA%\GameShelf\library.db`. Before import it parses the old library.json and writes a unique `library.json.pre-sqlite-*.bak` copy. The original JSON and screenshot assets remain untouched. Schema creation and import commit in one SQLite transaction. Existing SQLite databases are authoritative: subsequent starts do not reimport JSON. Unsupported schema versions fail closed.

Every save is transactional. `library.db.bak` is an SQLite online backup of the previous committed state. Close the app before manually restoring that file. Retain the entire GameShelf data folder, including screenshots and legacy JSON. Do not run the old JSON-based app to edit the same library after migration.

Each game now has archive and installation collections with stable IDs. Installations keep their own executable and optional source-archive ID. Game-level IDs, titles, notes, tag text, favorite/played flags, launch timestamp, cover paths and library roots migrate unchanged. Missing paths are retained rather than dropped.

Use **Add archive** to attach one or more ZIP/RAR/7z files. Use **Add folder** to append an existing installation. Choose **Archive to extract**, then Extract archive; each completed extraction adds another installation. Choose **Folder to launch from**, then Choose executable or Launch game. Existing folders and launch settings are retained. Repeated attachments of the same path to one game are ignored. Selectors default to the first location after restarting; selection preference is not persisted.

Attaching a path already listed on another game does not delete or merge that game's metadata. Those cards remain separate until a future explicit merge feature. Scanning recognizes all attached locations. No filesystem content is moved or deleted by linking.

Storage: relational games, archives, installations and roots tables. Game metadata is a versioned JSON payload in the games table; archive/install relationships and provenance use foreign keys. The current repository saves a full snapshot within one transaction and retains the existing single-instance UI policy. It is not a multi-user database or incremental large-library synchronization layer.

Validation: 94 checks passed on .NET 10, including migration backup/source retention, all scalar metadata, multiple paths and executable choices, rescan deduplication, transaction rollback, readable backup, corrupt-source protection and migration retry, plus the existing archive suite.
