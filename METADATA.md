# Metadata providers and review

ArcHive 0.3.0 supplies `IMetadataProvider`, an offline candidate importer and an optional VNDB adapter. Steam, IGDB, itch.io, RAWG and F95Zone network adapters are not implemented yet. No credentials are requested or persisted. Manual library use stays offline.

## Review workflow

Open **Find metadata** for the selected game. Search a title or import a JSON file, select one candidate, then choose individual fields. Existing values are unchecked; empty fields may be prechecked but still require Apply. Cancel discards the lookup. Repeat with another source to reconcile different fields. Title, description, tags, release year and engine are supported; provider descriptions remain plain text, including any source markup. Nothing renames files, launches games, combines library entries, polls versions or downloads artwork.

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

## Verification scope

Automated tests use deterministic HTTP fixtures; they require no network, secrets or provider availability. They cover request shape, mapping, caching, cooldown, rate limits, malformed/oversized responses, failure and cancellation alongside ranking, conflict review, stale-preview rejection, provenance, undo and SQLite persistence. Live service availability and full end-to-end UI behavior are not claimed by these checks.
