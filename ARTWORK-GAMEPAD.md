# Artwork and controller support — 0.5.0

## Artwork sources and selection

Metadata candidates may include image references with a role, HTTPS URL, provider, stable game ID and retrieval timestamp. No artwork is automatically selected during matching. In the review dialog, **Preview** downloads the image; **Use as Cover/Backdrop/Screenshot** queues a choice; **Apply** persists it. Cancel leaves selections unchanged (previewed images may remain in the expendable cache). Choose multiple roles or screenshots before applying. Selected role buttons are labeled; clicking a selected button removes that pending choice. A later choice replaces an earlier pending choice for the same cover/backdrop role; screenshot choices append, deduplicate by URL, and cap at 24 per game. Preview failure shows a neutral placeholder; a saved choice can retry on a later load.

The library card uses the cover. Game details use the backdrop, falling back to the cover, then the legacy local image/placeholder. Screenshot thumbnails appear in the Selected artwork panel. That panel identifies the selected cover/backdrop source and allows clearing roles or removing screenshots. These actions affect references only, not local files or other games. Text metadata Undo does not undo artwork selection; selections have their own explicit replacement/removal controls. Original local artwork remains available as a fallback.

| Provider | Images exposed by this build |
| --- | --- |
| IGDB | Cover, artwork/backdrops, screenshots using documented image IDs and sizes. |
| RAWG | Background and short screenshot URLs included in search responses. Any can be explicitly assigned to another role. |
| itch.io | Cover returned for an owned game or editable project. |
| VNDB | Cover and screenshots. |
| Steam | The supported catalog endpoint returns no artwork. No guessed CDN URLs or undocumented store scraping. Match another source or use local artwork. |

Official image references: [IGDB](https://api-docs.igdb.com/#images), [RAWG](https://api.rawg.io/docs/), [itch.io](https://itch.io/docs/api/serverside), [VNDB](https://api.vndb.org/kana). Existing provider attribution links remain in review and library views. Images and their rights remain with their respective owners; downloaded artwork is not committed to the repository.

## Download and cache behavior

- Selected/previewed images load asynchronously. Recycled/unloaded controls cancel requests and ignore obsolete completions. No provider API credentials or cookies are attached to image requests.
- Only HTTPS on exact provider CDN hosts is accepted: `images.igdb.com`, `media.rawg.io`, `img.itch.zone`, `img.itch.io`, `t.vndb.org`, `s.vndb.org`. Provider/source identity must match the candidate. Queries, fragments, user-info and custom ports are rejected. Redirects are disabled. Unsupported URLs are omitted from provider artwork without discarding text metadata.
- PNG and baseline/progressive JPEG downloads are limited to 12 MiB and 20 seconds, with a supported image MIME type. Dimensions are checked before native decoding: at most 8192 per side and 32 million pixels. Actual WPF decoding must succeed before publication; files claiming to be images are not accepted on MIME alone. Remote GIF/WebP/SVG are not supported. Existing local PNG/JPEG/BMP artwork remains supported within dimension/size limits.
- The dedicated `%LOCALAPPDATA%\ArcHive\artwork-cache` folder (or `ARCHIVE_DATA_DIRECTORY/artwork-cache`) uses SHA-256 URL filenames. Publication uses a unique temporary file and atomic move. A serialized queue deduplicates simultaneous requests and protects eviction. Failed/canceled downloads leave no partial published image; failures back off for 30 seconds. Old staging files are cleaned on cache maintenance.
- Disk images have a 256 MiB least-recently-used budget. Decoded images have a 64 MiB estimated pixel budget and at most 32 entries; long-side decoding is capped near 1280 pixels. Two display decoders run at a time. Disk cache hits are validated; corrupt entries are discarded and fetched again. Cached images remain usable offline across app restarts. An evicted image is downloaded again when needed; otherwise local artwork/placeholders remain.
- Artwork URLs/timestamps live in the existing schema-2 game JSON, independent of expendable cache files. There is no structural database migration or background whole-library prefetch. Cache content is not refreshed while an unchanged URL remains valid; a newly selected provider URL creates a separate entry. Cache instances are intended for one running app process, under a user-controlled local folder.

## Controller badges

Games have Unknown, None, Partial or Full controller support. Partial/Full show a gamepad-shaped vector badge on the card, with the exact value in its tooltip. This is a capability label, independent of whether a controller is currently connected. Existing games default to Unknown. The detail selector is editable and records Manual provenance. RAWG's explicit `full-controller-support`/`partial-controller-support` tag slugs can supply reviewable hints; absence does not mean None. Other adapters do not infer support from engine, platform or artwork. Controller field changes participate in metadata review and protected undo.

## Gamepad navigation and launching

Windows [XInput](https://learn.microsoft.com/en-us/windows/win32/xinput/getting-started-with-xinput) is polled every 60 ms. The first connected controller among four slots drives ArcHive. DirectInput-only controllers are not supported natively; a compatible mapping may expose them as XInput. The sidebar toggle controls navigation for this session.

- D-pad or left stick: select within the visible filtered grid/list, scrolling selected rows into view. The selected card is highlighted. The stick has a 12,000-unit dead zone; directional repeats start after 450 ms and repeat at 120 ms intervals.
- Shoulder buttons: cycle All games, Favorites and Archives.
- A: open the existing selected installation/profile's launch confirmation. The same confirmation is used for mouse launching and shows exact command/working directory. Release buttons, then press A again to launch, or B to cancel. A is edge-triggered and never repeats while held.
- Input is ignored when ArcHive is inactive, disabled by a modal dialog, busy with an operation, or editing a text/password field or selector. Return focus to the game grid to resume. Reconnection, controller-slot change, focus return and confirmation windows require neutral input before accepting actions. Launch setup still requires a valid explicit executable/profile; the badge does not bypass checks.

No buttons are injected into a game and no game input mappings are modified. Automated tests exercise the navigation state machine, actual Windows image decoders, cache and persistence logic. Physical-controller behavior, accessibility, layout and full end-to-end UI flows were not tested in this milestone.
