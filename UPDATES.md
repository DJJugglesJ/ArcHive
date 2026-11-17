# ArcHive application updates

Open **Settings > Updates > Check for Updates**. ArcHive checks the latest stable packaged release in DJJugglesJ/ArcHive. Optional **Check for Updates at Startup** shows an availability message in the main window; it is off by default. No GitHub login, API key, game path or library contents are sent.

## Install flow

1. Check the published version and release notes.
2. Download Update stages the Windows x64 MSI in Windows temporary storage (`ArcHive/Updates`) in 1.0.3. **Change…** selects another local download folder for the current dialog; changing it clears install readiness and requires a fresh verified download. Released 1.0.1/1.0.2 use the library data `updates` folder. Progress and Stop are available; Circle cancels an active check/download.
3. ArcHive verifies the exact size and SHA-256 digest supplied by GitHub. Failed/canceled partial downloads are removed. Equal or older releases cannot be downloaded through the updater.
4. Close ArcHive and Install rechecks the staged file. Running tracked games and library operations block installation. The library and preferences are saved before ArcHive closes; a canceled/failed close does not start installation.
5. Windows Installer opens interactively. Reopen ArcHive from the Start menu after completion. MSI upgrades use the existing installation location and preserve the separate library data. A portable copy installs the managed edition alongside its existing folder; it does not rewrite the portable folder.

The updater does not pull source commits, run repository scripts or build code. A source change becomes available to installed users when a new stable GitHub release includes `ArcHive-X.Y.Z-win-x64.msi` with a valid GitHub SHA-256 asset digest. Drafts, prereleases, malformed releases, foreign assets and unsupported package sizes are rejected. Public requests are bounded, cancellable and HTTPS-only; redirects are restricted to GitHub asset hosts. No automatic retry loop runs.

The digest detects corruption and substitutions relative to GitHub's published metadata; it is not a separate publisher signature. Release publishing and repository access remain the trust boundary. Current packages are unsigned. Staged verified installers remain available after failed/canceled Windows installation; no successful install is claimed merely because the installer was started.

## Verification and limits

Automated tests cover stable release selection, foreign URLs, missing digests, downgrade prevention, size/hash validation, redirected hosts, cancellation, partial cleanup, staged tampering, alternate download folders, retry isolation and preference persistence.

The actual installed 1.0.1 discovered and downloaded published 1.0.2. The first handoff failed with Windows Installer error 2755/1619: its service could not open this machine's AppData staging path. Repeating the complete updater workflow with a library copy in the workspace succeeded; Windows Installer recorded product 1.0.2, status 0. All 406 installed files match the published package. Original library values, settings and artwork were preserved; SQLite integrity and foreign-key checks passed.

Version 1.0.3 moves staging out of the library, offers another local folder, and writes an adjacent `.install.log` when handing off to Windows Installer. A real Windows Installer administrative extraction from the new temporary location completed with status 0. This validates package access without performing another application upgrade. These changes are not in the unchanged v1.0.2 release assets.

If released 1.0.2 encounters error 2755, download the MSI from the release page to a local folder accessible to Windows Installer and run it after closing ArcHive. Do not delete or move your library to work around installer staging.

API contract: [GitHub release endpoints](https://docs.github.com/en/rest/releases/releases#get-the-latest-release).
