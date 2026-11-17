# ArcHive application updates

Open **Settings > Updates > Check for Updates**. ArcHive checks the latest stable packaged release in DJJugglesJ/ArcHive. Optional **Check for Updates at Startup** shows an availability message in the main window; it is off by default. No GitHub login, API key, game path or library contents are sent.

## Install flow

1. Check the published version and release notes.
2. Download Update stages the Windows x64 MSI in the library data `updates` folder. Progress and Stop are available; Circle cancels an active check/download.
3. ArcHive verifies the exact size and SHA-256 digest supplied by GitHub. Failed/canceled partial downloads are removed. Equal or older releases cannot be downloaded through the updater.
4. Close ArcHive and Install rechecks the staged file. Running tracked games and library operations block installation. The library and preferences are saved before ArcHive closes; a canceled/failed close does not start installation.
5. Windows Installer opens interactively. Reopen ArcHive from the Start menu after completion. MSI upgrades use the existing installation location and preserve the separate library data. A portable copy installs the managed edition alongside its existing folder; it does not rewrite the portable folder.

The updater does not pull source commits, run repository scripts or build code. A source change becomes available to installed users when a new stable GitHub release includes `ArcHive-X.Y.Z-win-x64.msi` with a valid GitHub SHA-256 asset digest. Drafts, prereleases, malformed releases, foreign assets and unsupported package sizes are rejected. Public requests are bounded, cancellable and HTTPS-only; redirects are restricted to GitHub asset hosts. No automatic retry loop runs.

The digest detects corruption and substitutions relative to GitHub's published metadata; it is not a separate publisher signature. Release publishing and repository access remain the trust boundary. Current packages are unsigned. Staged verified installers remain available after failed/canceled Windows installation; no successful install is claimed merely because the installer was started.

## Verification and limits

Automated tests cover stable release selection, foreign URLs, missing digests, downgrade prevention, size/hash validation, redirected hosts, cancellation, partial cleanup, staged tampering and preference persistence. Live API and desktop checks confirm the latest published version is 1.0.0, older than source 1.0.1, with Download/Install disabled. A full live upgrade through this new updater awaits a newer published package. Existing installer packaging and prior install/uninstall evidence are documented separately in VALIDATION.md.

API contract: [GitHub release endpoints](https://docs.github.com/en/rest/releases/releases#get-the-latest-release).
