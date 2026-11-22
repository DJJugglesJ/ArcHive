# Browsing preferences and updater verification

- 802 automated checks passed. New coverage includes tag search/counts/order, exact blacklist matching, duplicate-title identity, restoration, draft isolation, older settings compatibility and persistence.
- Focused isolated UI checks passed: search, favorite and blacklist a tag; save and reopen; favorite and blacklist a game; verify hidden-but-retained library state and persisted game favorite. SQLite integrity and foreign-key checks passed.
- v1.0.3 updater discovered, downloaded and verified v1.0.4 using temporary staging. Windows Installer completed successfully (status 0). All 406 installed files match the published package; the upgraded app reopened normally.
- Library rows, settings and artwork compared exactly with the pre-upgrade backup before and after startup. Development UI checks used a separate fixture.
- Release build succeeded. NU1900 warnings indicate that NuGet vulnerability data was unavailable; it was not refreshed.
- No broad UI sweep or physical-controller test. New browsing features are source changes for the next release, not modifications to published v1.0.4 assets.
