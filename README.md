# ArcHive — standalone game library

![Approved ArcHive logo and icon](ArcHive.App/Assets/ArcHive-brand-approved.png)

RAR/7z extraction is enabled by default alongside ZIP. Build with the .NET 10 SDK:

```powershell
dotnet build ArcHive.App/ArcHive.App.csproj -c Release
dotnet run --project ArcHive.App/ArcHive.App.csproj
dotnet run --project ArcHive.Tests/ArcHive.Tests.csproj
```

Add a library folder, select a game, and use Extract archive. Choose a new destination parent; the app creates a unique game directory. Select the executable explicitly before launching. Tags, favorites, played status, notes and cover images are stored in %LOCALAPPDATA%\ArcHive. Scanning never launches games.

Extraction uses pinned SharpCompress 0.50.9 for RAR/7z and the existing ZIP adapter. Solid archives are read sequentially; 7z entry order is reconciled with the preflight plan. Paths, duplicate names, declared and actual output size are checked before publication. Files are created in staging without overwriting existing games. Ordinary failures and cancellation clean staging. Encrypted and incomplete archives are unsupported. Original archives are retained.

This is not a malware sandbox. Extract into a user-controlled local directory without hostile concurrent filesystem changes. Limits bound output size and entry count, not decoder memory or CPU; cancellation cannot interrupt a decoder until its read returns. Crash recovery, full format-specific link metadata coverage, checksum-corruption corpus testing and GUI validation remain release-hardening work. No archive metadata is used to create filesystem links or restore special attributes.

See VALIDATION.md for exact tests. ARCHITECTURE.md retains the original roadmap; its first-increment status descriptions are historical.


## ArcHive continuation

The project, namespaces and build output now use ArcHive throughout. See [ROADMAP.md](ROADMAP.md) for implemented/planned features, [design/DESIGN.md](design/DESIGN.md) for the approved branding, and [CONTINUATION.md](CONTINUATION.md) for library preservation.

The adaptive artwork grid, background thumbnails, optional details and archive view are implemented. New scans clean CamelCase/snake_case and explicit version/platform labels without renaming originals or overwriting edited titles. Online providers and advanced roadmap features remain planned.

## Windows packaging

Run `powershell -NoProfile -File packaging/Build-Installer.ps1` to publish the self-contained Windows x64 app and build the per-user MSI. Output: `artifacts/publish/win-x64/ArcHive.exe` and `artifacts/installer/ArcHive-0.1.1-win-x64.msi`. No separate .NET runtime is required by the installed application. Build prerequisites and verification commands are in [packaging/README.md](packaging/README.md).

The approved icon is embedded in ArcHive.exe, the Start Menu shortcut and the Installed Apps entry. The sidebar blends the original logo into its surface without a black image rectangle. Windows uses its standard file-type icon for the .msi file itself. The package is currently unsigned.
