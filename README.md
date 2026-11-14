# ArcHive — standalone game library

![Approved ArcHive logo and icon](GameShelf.App/Assets/ArcHive-brand-approved.png)

RAR/7z extraction is enabled by default alongside ZIP. Build with the .NET 10 SDK:

```powershell
dotnet build GameShelf.App/GameShelf.App.csproj -c Release
dotnet run --project GameShelf.App/GameShelf.App.csproj
dotnet run --project GameShelf.Tests/GameShelf.Tests.csproj
```

Add a library folder, select a game, and use Extract archive. Choose a new destination parent; the app creates a unique game directory. Select the executable explicitly before launching. Tags, favorites, played status, notes and cover images are stored in %LOCALAPPDATA%\GameShelf. Scanning never launches games.

Extraction uses pinned SharpCompress 0.50.9 for RAR/7z and the existing ZIP adapter. Solid archives are read sequentially; 7z entry order is reconciled with the preflight plan. Paths, duplicate names, declared and actual output size are checked before publication. Files are created in staging without overwriting existing games. Ordinary failures and cancellation clean staging. Encrypted and incomplete archives are unsupported. Original archives are retained.

This is not a malware sandbox. Extract into a user-controlled local directory without hostile concurrent filesystem changes. Limits bound output size and entry count, not decoder memory or CPU; cancellation cannot interrupt a decoder until its read returns. Crash recovery, full format-specific link metadata coverage, checksum-corruption corpus testing and GUI validation remain release-hardening work. No archive metadata is used to create filesystem links or restore special attributes.

See VALIDATION.md for exact tests. ARCHITECTURE.md retains the original roadmap; its first-increment status descriptions are historical.


## ArcHive continuation

See [ROADMAP.md](ROADMAP.md) for the approved roadmap and implemented/planned status, [design/DESIGN.md](design/DESIGN.md) for the UI and approved original branding, and [CONTINUATION.md](CONTINUATION.md) for preservation and architecture decisions. The existing WPF implementation and GameShelf storage location are retained. The Release executable is `GameShelf.App/bin/Release/net10.0-windows/GameShelf.App.exe` and its window is titled ArcHive.

The library now has an adaptive artwork grid, background thumbnails, optional details and an archive-only view. New scans clean CamelCase/snake_case and explicit version/platform labels without renaming original files or replacing existing edited titles. Online providers and advanced roadmap features are not yet implemented.

Verified launch fallback (from GameShelf): `dotnet GameShelf.App/bin/Release/net10.0-windows/GameShelf.App.dll`. See VALIDATION.md for this increment's live checks and deployment limitations.

The original approved branding is embedded unchanged; the app displays its logo and icon regions. See design/DESIGN.md for provenance and packaging status.
