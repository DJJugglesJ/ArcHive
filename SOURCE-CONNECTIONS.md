# Sources and owned-library import

## Credential policy

API keys and client secrets persist only in Windows Credential Manager (`ArcHive/MetadataProviders`). They never enter ArcHive's SQLite metadata, settings JSON, snapshot files, or application logs. The existing Windows vault format remains compatible. Provider credentials exist in process memory while needed; Windows owns their at-rest storage.

Settings → Sources edits one provider at a time. Saving preserves other sources; Disconnect clears the selected source and invalidates provider caches. Remember securely in Windows is the default for a new configuration. Disabling it makes all configured keys session-only and removes the saved aggregate Windows credential; the UI explains this scope.

No Steam, GOG or forum password is requested. Browser sign-in uses fixed HTTPS links in the user's default browser. ArcHive does not scrape browser cookies, embed a persistent login webview, or mark browser sign-in as a connected account. Credential setup is not itself an authentication check.

## Capabilities

| Source | In-app access | Login/setup |
|---|---|---|
| Steam | Catalog metadata; optional visible owned-library fetch | User API key in Windows vault; SteamID64 entered only for the import request |
| IGDB | Title search and supported artwork | User's registered Twitch client ID/secret; token exchange stays in memory |
| itch.io | Owned games / projects metadata | User API key in Windows vault |
| RAWG | Metadata and supported artwork | User API key in Windows vault; attribution retained |
| VNDB | Title metadata | No credentials required |
| F95Zone | Explicit linked-thread public page metadata, observed version | Opt-in; browser login only; no authenticated/private-page access |
| GOG | Browser account access; optional owned-library JSON import | No in-app OAuth session or authenticated library fetching |

Steam account authentication and Web API access are different capabilities: [Steam authentication documentation](https://partner.steamgames.com/doc/features/auth). Owned games are fetched using [IPlayerService/GetOwnedGames](https://partner.steamgames.com/doc/webapi/IPlayerService#GetOwnedGames), subject to account visibility. GOG documents [OpenID integration](https://docs.gog.com/sdk-openid/); this increment does not register an ArcHive OAuth client or use another application's credentials.

## Optional owned-library import

Available through Add games or Settings → Sources. Review and select titles before adding them. Existing entries are not overwritten, and title similarity alone never merges two games. Repeated imports deduplicate by provider + ID. Imported entries contain no executable path and show Not installed until linked to local files.

Steam can fetch using the configured API key and a 17-digit SteamID64, or import a JSON response shaped as:

```json
{"response":{"games":[{"appid":42,"name":"Example game"}]}}
```

GOG supports a local JSON import shaped as:

```json
{"products":[{"id":99,"title":"Example game"}]}
```

This describes the supported interchange format, not a promise of a built-in GOG export button or automatic sync. Supply library data you already have in that shape. Files are limited to 2 MB and 10,000 rows. Only normalized provider IDs and titles are retained; extra export fields are discarded. API requests are explicit, bounded, cancellable and do not follow cross-host redirects.

## F95Zone

Enable Settings → Optional sources → F95Zone thread tools. Open a game → Sources → F95Zone thread / updates, paste its thread link, and save it or check its public page.

- Only HTTPS `f95zone.to/threads/...` links are accepted. Stored links use a numeric thread ID; URL queries and fragments are discarded.
- Lookups send no cookies, authorization headers or passwords. Only up to two same-thread redirects are followed. Login/challenge pages, wrong canonical identities, unsupported destinations and oversized responses fail without changing game metadata.
- Public HTML metadata is parsed as plain text, never rendered/executed. Requests have time, size and pacing limits. No attachment/download extraction or site-wide search is implemented.
- The candidate retains provider identity and observation time. Review metadata uses the existing confidence/field reconciliation screen. Manual values stay unchanged unless explicitly selected.
- A conservative bracketed version is evidence, not an automatic update. The user can explicitly use it as latest known version; installed versions remain untouched.
- Saving a link persists the approved association and reviewed changes. Cancelling the dialog discards its detached changes.

The site's terms/API pages could not be verified through the available web reader during implementation. No documented F95Zone account API is assumed. Synthetic pages test the parser and HTTP boundaries; authenticated live lookup and site markup compatibility remain unverified. If public access is blocked, use the browser; ArcHive does not bypass the block.
