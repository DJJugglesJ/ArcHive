# ArcHive workflow design — 0.11.0

The goal is to make the next useful action obvious, keep secondary tools nearby, and show what every connection actually provides. The approved branding and artwork-first collection remain intact.

## User journeys

| What the user wants | Entry point | Behavior |
|---|---|---|
| Add downloaded games | Add games → Scan a folder | Recursively scan in place; do not move originals. |
| Bring in owned titles | Add games → Import owned Steam / GOG titles | Preview, select explicitly, deduplicate stable source IDs, add uninstalled entries. |
| Find a game | Collection search, status filter, Favorites | Preserve the selected game when still visible; explain a filtered-empty view. |
| Browse with a controller | D-pad / stick, Cross/A | Open details without launching; Triangle/Y opens options. |
| Play an installed game | Game details primary action | Validate the selected launch profile before execution. |
| Prepare an archive-only game | Extract to play / Preview queue | Inspect contents and queue extraction. |
| Fix a folder-only game | Set up launch → Files & launch | Choose an executable or review local detection evidence. |
| Link an uninstalled/imported title | Link game folder | Explicitly associate an existing installation; no automatic download. |
| Read about a game | Overview | Read-only description first; expand Edit game details when wanted. |
| Edit status/tags/notes | Overview → Edit game details | Keep direct user edits and the existing save flow. |
| Change imagery | Artwork | Existing provider/local artwork selection and attribution. |
| Manage versions/profiles | Versions / Files & launch | Keep installed and latest-known versions distinct; preserve default launch profiles. |
| Check a source match | Sources → Find metadata | Review similarity evidence and field-level conflicts; apply chosen changes only. |
| Follow an F95Zone thread | Enable optional tools, then game Sources or Options | Explicit thread link, public lookup, reviewed metadata and optional latest-version update. |
| Protect or restore saves | Saves → Manage save backups | Snapshot, preview restore, protected pre-restore backup, recovery. |
| Watch extraction work | Sidebar Activity | Existing queue, cancel/retry and crash recovery. |
| Organize many entries | Sidebar Organize library | Existing duplicates and previewed batch operations. |
| Configure the app | Settings | Library, Sources, Controls, Storage, Optional sources and About. |
| Tune browsing | Settings → Library | Persist title/recent/favorites ordering and comfortable/compact artwork grid. |
| Manage API credentials | Settings → Sources | One provider at a time; explicit save/disconnect; no false Connected status. |
| Sign in to websites | Sources → Open website / sign in | Default browser handles login; this is not an ArcHive account connection. |
| Locate local data | Settings → Storage | Show compact path, expose full tooltip, open the data folder. |

## Interaction and visual rules

- The collection toolbar concentrates on adding games and rescanning. Activity and Organize live in navigation.
- Details keep the contextual primary action, archive preview and Options near the title. Overview, Artwork, Versions, Files & launch, Sources and Saves separate tasks without a wall of forms.
- Opening details focuses the primary action after tab layout completes. L1/R1 (LB/RB) switches details tabs; in the collection it switches views. Physical controller verification is still outstanding.
- Selected toggle buttons keep accessible checked state. Settings tabs, password inputs and disclosures use the dark ArcHive palette.
- Website login and API credential setup are separate operations. An API key is labeled configured, not verified; saving it makes no request.
- Sources save explicitly inside their section; Save preferences applies library/control/optional-source preferences. Browser sessions belong to the browser, not ArcHive.

## Deliberate boundaries

This is a workflow refactor of the existing desktop app, not a claim that every roadmap item is complete. Full controller operation of every modal, automatic save discovery, storefront downloading/installing, cloud synchronization, GOG authenticated owned-library fetching and bulk metadata matching are not implemented. Existing completed features remain available through their mapped routes.

A focused layout review covered the collection, details and expanded Settings. Automated tests cover the changed logic; this was not a new full end-to-end or physical-controller test run.
