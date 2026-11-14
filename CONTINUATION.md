# ArcHive continuation

The working repository is now ArcHive/ with ArcHive.App, ArcHive.Core and ArcHive.Tests. All current source names, namespaces, projects and documentation use ArcHive. Git history and the user's original commit are preserved.

The application remains C#/.NET 10 WPF with an independent core. Default data is %LOCALAPPDATA%/ArcHive. Existing local library data was copied and integrity-checked during this rename, with the original retained as a backup. Other users upgrading from a pre-rename build should close the app and copy their previous data folder to this new location before first launch; do not overwrite an existing destination. The ARCHIVE_DATA_DIRECTORY override remains available for portable or test libraries.

The original branding board is preserved unchanged. The UI screen-composites its logo over the sidebar, and a reproducible nine-size ICO packages its approved icon into the executable and MSI. See packaging/README.md for release builds and verification.

ROADMAP.md tracks implemented and planned features. Historical architecture and test records remain available. No user databases, test installs, downloaded compilers or build outputs are included in Git.
