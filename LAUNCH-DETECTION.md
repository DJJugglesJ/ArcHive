# Launch suggestions and UI verification (0.9.0)

## Evidence before changes

Select a game and choose **Launch / engine suggestions**, either from details or the options menu. Select an installation and press **Scan installation**. Scanning reads directory/file names without running executables or loading their assemblies. Links are skipped, support folders are excluded, inaccessible paths are reported, and traversal is limited to 20,000 entries and 12 levels below the root. Limits produce an incomplete-results note. Cancel/close cancels an active scan.

Candidates list executable paths, engine hints and observed evidence. Selecting a row exposes its full path and evidence in the notes area. Ranking favors root-level executables and matching engine markers; it is a heuristic, not a probability or proof of compatibility. Multiple candidates and multiple engine signatures are explicitly flagged. Installers, uninstallers, crash handlers, redistributables, common runtime helpers and known support directories are excluded. Unrecognized helpers can still appear, so manual review remains necessary.

| Hint | Evidence used |
| --- | --- |
| Unity | Executable-specific `_Data` folder plus adjacent `UnityPlayer.dll` |
| Ren'Py | Adjacent `renpy` and `game` folders |
| RPG Maker MV / MZ | `js/rpg_core.js` / `js/rmmz_core.js`, directly or beneath `www` |
| Godot (possible) | Adjacent matching executable-name `.pck` |
| Unreal Engine (possible) | Executable ending `-Shipping` inside a nested `Binaries/Win64` path |

These signatures are deliberately limited. Embedded Godot packs, renamed runtime files, older Unity exports and uncommon layouts may remain Unknown. Mixed-engine distributions require choosing the relevant paths. Marker files are evidence, not executable validation or a security verdict. Official format references include [Unity Windows build output](https://docs.unity.com/en-us/engine/6000.7/manual/platform-specific/windows/standalone-binaries), [Godot Windows export](https://docs.godotengine.org/en/stable/tutorials/export/exporting_for_windows.html), and [Ren'Py game-directory paths](https://renpy.org/dev-doc/html/language_basics.html).

All apply checkboxes begin unchecked:

- **Add selected executable** adds a named profile with an empty argument list and executable-directory working folder. Existing profiles, arguments and default choice remain unchanged. If the executable already has a profile, it is not duplicated. A first profile becomes the default only when no profiles exist.
- **Apply selected engine** explicitly replaces the current engine. Scanning and accepting only a profile leave a manual engine untouched.
- **Apply suggested title** explicitly replaces the display title with the reviewed source-path suggestion. It never renames files or folders.

Changes are prepared on a copy, launch paths are checked again before adding a profile, and the library is replaced only after SQLite saves successfully. Accepted evidence is persisted as up to 20 local detection decisions per game. Existing libraries retain their metadata and need no schema migration. Current fields are shown above suggestions; prior accepted evidence appears when opening the dialog.

## Numeric names and source locations

New discoveries with numeric-only names now retain the numeric identifier and include a meaningful immediate-parent name when available: `MoonGarden/123` becomes `Moon Garden · 123`. Numeric-only parents do not invent a name. Existing entries and manually edited titles are not renamed by rescans; source-based corrections require review. Ordinary title parsing remains conservative.

Full archive/installation paths are available in the read-only source panel near the top of details, card tooltips, archive-list source column, and organizer source column/selected-entry evidence. **Open source folder** opens the selected installation or archive's parent in Explorer after checking that it exists and is not a linked path. Source identity stays separate from the editable title.

## Options and selection

Right-click a library card or archive row to select that item and open its options. Triangle on DualSense/DS4, or Y on Xbox, opens options for the current selection. D-pad/stick moves through enabled menu choices, Cross/A activates, and Circle/B or Triangle/Y closes. Holding the north button does not repeatedly activate the menu. Keyboard users can use Shift+F10 or the context-menu key; library arrows share the same navigation handler as the gamepad.

Available actions include launching the current profile, local launch/engine review, archive preview/queue, source folder, metadata and organization. Inapplicable launch/archive actions are disabled. A controller action in an open menu is routed to the menu instead of inadvertently launching the underlying game.

Archive selection now synchronizes the visible list with the shared selected game and scrolls into view. A green border and fill remain visible without keyboard focus. Cards honor their thicker selected border, tables keep contrast for inactive selection, and secondary windows use explicit dark backgrounds. Dropdowns, menus and tooltips are themed; the main toolbar wraps on its own row instead of crowding the title. Organizer selection exposes full source paths, and evidence notes expose untruncated candidate details.

## Automated and live verification

511 automated checks pass (469 previous plus 42 detection/input/display checks), and the Release WPF build has zero warnings/errors. Tests cover supported signatures, excluded helpers, ranking/conflicts, cancellation/bounds, source-context parsing, preservation of manual choices/defaults/arguments, vanished candidates, explicit replacement, source immutability, evidence persistence and DualSense/DS4 Triangle press/hold behavior.

The requested live desktop test used an isolated `ARCHIVE_DATA_DIRECTORY` with local fixture archives, engine-marker files and a harmless compiled executable that writes a success marker. No real game library was modified. Verified workflows:

- Right-click on cards and archive rows selects the intended item and opens options.
- Archive navigation through the shared keyboard/controller handler moves the visible highlight and inspector source together.
- Local scanning displays Unity evidence and excludes its crash helper; accepting a new candidate and source title preserves the manual engine and default launch profile. SQLite inspection confirms two profiles, preserved default and recorded evidence.
- Launching afterward runs the preserved default and produces the expected success marker.
- Organizer multi-selection, before/after preview and Apply persist favorites for the three visible archive entries while leaving the non-archive entry unchanged.
- Archive content preview displays the fixture's `readme.txt` and expected sizes with the repaired dark theme.
- Restart, smaller-window layout and the context-menu keyboard shortcut are checked on the final build.

The live test exposed white secondary-window backgrounds beneath light text; explicit window colors fix this rather than relying on a base Window implicit style. It also prompted tooltip, row-header and full-evidence refinements. Physical controller hardware was not available: Sony button decoding and held-button behavior are automated tests, while visible selection/menu routing is exercised on the shared UI path. This is not a claim of hardware or all-device accessibility verification.

### Details-first release polish

Left-click or Cross/A opens a dedicated details page; Play is a separate action. Right-click or Triangle/Y opens options. Boolean controls use selected buttons with accessible checked state. Display paths retain the drive and final two components, with full paths available through tooltips and Copy full paths. Stored paths remain unchanged.

Live fixture verification confirmed opening details did not change the launch marker, explicit Play did, and the details layout fit a 1014-pixel-wide window. Automated checks cover compact drive/UNC paths, unchanged stored paths, and held-accept suppression.
