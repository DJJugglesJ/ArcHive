# Metadata providers and review

ArcHive 0.4.0 supplies `IMetadataProvider`, offline import and optional VNDB, Steam, IGDB, itch.io and RAWG adapters. All searches are explicit and use the existing confidence/field-review workflow. F95Zone remains planned. Manual library use stays offline.

## Review workflow

Open **Find metadata** for the selected game. Search a title or import a JSON file, select one candidate, then choose individual fields. Existing values are unchecked; empty fields may be prechecked but still require Apply. Cancel discards the lookup. Repeat with another source to reconcile different fields. Title, description, tags, release year, engine and controller support are supported; provider descriptions remain plain text, including any source markup. Text reconciliation does not rename files, launch games, combine entries or poll versions. In 0.5.0, artwork URLs are included where available; Preview explicitly downloads an image and role selection plus Apply saves it. See [ARTWORK-GAMEPAD.md](ARTWORK-GAMEPAD.md).

Each accepted field stores its provider, stable provider ID and retrieval time. Later edits through the detail panel record Manual provenance. Existing fields with no historical attribution display as existing/local. Undo retains up to 20 applied batches across restarts and restores previous values/provenance atomically. Undo refuses to overwrite subsequent changes; there is no destructive force-undo. A stale preview must be reviewed again. Metadata is additive in the schema-2 game JSON document, with existing transactional saves and backups; no SQL schema upgrade is required. Older app builds do not preserve new fields when saving, so retain a backup before downgrading.

## Matching rules

- Unicode compatibility normalization, case folding and punctuation tokenization; sequel numbers and accents are retained.
- Exact normalized title or alias contributes 0.8. Otherwise the best title/alias token Jaccard similarity contributes up to 0.8.
- Known year and engine contribute 0.1 each when equal, or subtract 0.25 each when conflicting. Missing evidence contributes nothing. Clamp to 0–1.
- Duplicate provider/ID pairs collapse; separate IDs and providers remain distinct. Candidates within 0.1 score of another are marked ambiguous. Sorting is deterministic.
- The displayed score is an explainable heuristic, not a calibrated probability. Even the highest-scoring match requires selection and field review; no threshold automatically applies metadata.

## Offline import

Import a UTF-8 JSON array (maximum 2 MB, 500 candidates). Use stable source IDs and truthful retrieval timestamps. Source labels in imported files are user-supplied claims, not authenticated attribution. Missing fields do not erase existing data. Example:

```json
[
  {
    "Provider": "local-catalog",
    "Id": "moon-garden-2020",
    "Title": "Moon Garden",
    "Aliases": ["Lunar Garden"],
    "Fields": {
      "Description": "A fictional example game.",
      "Tags": "adventure, puzzle",
      "ReleaseYear": "2020",
      "Engine": "RenPy"
    },
    "RetrievedAt": "2026-11-14T12:00:00Z"
  }
]
```

Invalid identities, unknown fields, oversized strings and malformed documents are rejected. Imported text is never treated as executable instructions or markup.

## VNDB adapter

The explicit search uses the documented [VNDB Kana API](https://api.vndb.org/kana), querying visual-novel titles and receiving identifiers, titles/aliases, descriptions and release dates. A release year is catalog metadata, not an installed game version. Search retrieves at most 20 candidates; refine the title if needed. Engine and tags are not inferred from this response.

Only the search title is sent. No credentials, paths, notes or library-wide uploads are used. Requests have a 20-second deadline and 2 MB response limit. The adapter serializes requests, imposes a two-second cooldown, honors bounded rate-limit backoff, and caches up to 100 successful title searches in memory for ten minutes. Cache timestamps remain the original retrieval time. There is no automatic retry or background polling. Closing the dialog cancels the request. Failures do not modify the library. The desktop HTTP client disables redirects.

VNDB API usage is subject to its [usage terms](https://api.vndb.org/kana#usage-terms) and [data license](https://vndb.org/d9). Review those terms before distributing commercial products or redistributing data. The UI identifies VNDB and preserves field attribution. Other adapters should use supported APIs and keep credentials out of the library, logs and repository.

## Additional provider scopes (0.4.0)

| Provider | Source and supported fields | Setup |
| --- | --- | --- |
| Steam | Official `IStoreService/GetAppList` catalog, filtered to games: app ID and title only. Catalog title matching happens locally; no undocumented store-detail scraping. | Your Steam Web API key. |
| IGDB | `/v4/games` title search: ID, title, aliases, summary, release year, engine names and genres. | Your Twitch application's client ID and client secret. ArcHive obtains a client-credentials app token, renews it before expiry (at most one hour), and retries once after 401. No shared application secret ships. |
| itch.io — owned games | `/profile/owned-keys`: stable game IDs/titles and short text when available. Reads only published games represented by owned keys. Unclaimed bundle games may be absent. Private purchase/key details are discarded. | Your itch.io key or scoped token with `profile:owned`. |
| itch.io — projects | `/profile/games`: games you uploaded or can edit; IDs/titles/short text. | Your itch.io key or scoped token with `profile:games`. |
| RAWG | `/api/games` title search: ID, title, released year, genre/tag names. Does not fetch a detail page or description per candidate. | Your RAWG API key. |

itch.io account scopes are not a public catalog search. Its publication dates are not treated as game release dates. Steam modified timestamps are likewise not release dates. Every provider result still requires candidate selection and per-field approval. Source links appear in review and library views; provenance records the originating provider and ID.

### Credentials and request handling

Open **Provider settings**, enter your credentials, and save. Blank keys disable the corresponding provider. The default is session-only; checking **Remember in Windows Credential Manager** stores a small JSON credential blob for the current Windows account, outside the game database and project files. Unchecking it removes saved settings; **Forget all keys** removes saved settings and clears the session providers/caches. Existing library metadata remains. The Windows credential blob is limited to 2,560 bytes. Credentials remain in process memory while used; this is not protection from software already running as your Windows user. Do not distribute your own client secrets with the app.

IGDB sends client credentials in a Twitch token POST form and its resulting bearer token in game-query headers. itch.io uses an Authorization bearer header. Steam and RAWG use the documented key query parameter over HTTPS; transport errors never include request URLs, response bodies, inner exceptions or credentials. No provider receives archive paths or notes. Steam/itch matching does not send the search title: it downloads the explicitly selected catalog/account scope and searches locally.

The four adapters share a host-allowlisted transport with redirects disabled by the desktop client, 20-second HTTP deadlines, 2 MB response limits, cancellation, request spacing and capped rate-limit backoff. Each search is limited to three minutes. IGDB requests are spaced by 350 ms; Steam, itch.io and RAWG by one second. Search results cache for ten minutes (up to 100 title queries). Complete Steam catalogs cache for six hours in memory; complete itch.io catalogs for ten minutes. Catalogs and tokens are not saved to disk. Changing settings resets all provider instances.

Steam pages use an advancing app-ID cursor, at most 50 pages of 10,000 entries. itch.io owned keys continue through short pages until an empty page, at most 100 pages of 500 keys; duplicate game IDs collapse while repeated key IDs fail the lookup. Editable-project lists are limited to 10,000 entries. Count, cursor, size or deadline failures do not publish a partial catalog. Returned pagination URLs are never followed. IGDB and RAWG return the first 20 search candidates; refine the title for more precise results. Catalog matching is performed off the UI thread, with a linear ambiguity check after sorting rather than pairwise comparisons.

### Official references and attribution

- [Steam IStoreService](https://partner.steamgames.com/doc/webapi/IStoreService) and [Web API authentication](https://partner.steamgames.com/doc/webapi_overview/auth).
- [IGDB API](https://api-docs.igdb.com/) and [Twitch token flow](https://dev.twitch.tv/docs/authentication/getting-tokens-oauth/#client-credentials-grant-flow).
- [itch.io server-side API and key setup](https://itch.io/docs/api/serverside).
- [RAWG API key, terms and attribution](https://rawg.io/apidocs). Review your current plan and terms before distribution; the UI provides active RAWG backlinks wherever its metadata is shown.
- [Windows Credential Manager API](https://learn.microsoft.com/en-us/windows/win32/api/wincred/nf-wincred-credwritew).

## Verification scope

Automated tests use deterministic HTTP fixtures; they require no network, secrets or provider availability. They cover request shape, mapping, caching, cooldown, rate limits, malformed/oversized responses, failure and cancellation alongside ranking, conflict review, stale-preview rejection, provenance, undo and SQLite persistence. Additional tests cover all four adapters, credential scoping, token renewal, short/duplicate/cyclic pagination, response limits and sanitized errors. Windows Credential Manager is exercised with a unique test-only credential that is deleted afterward; run this part in a normal Windows logon session (restricted/service sessions may lack a credential vault). Live service availability and full end-to-end UI behavior are not claimed by these checks.
