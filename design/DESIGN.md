# ArcHive design system

## Approved identity and source

The user supplied the selected SECOND black-and-green retro hacker variant on 2026-11-14. The arcade cabinet, honeycomb geometry, terminal-style ArcHive lettering and rounded-square icon are preserved exactly in `ArcHive.App/Assets/ArcHive-brand-approved.png`.

SHA256: `0AF19F0406E8390F73C0C442B063DB8F1210197F67ADEAF8F446E44400CEAC1C`.

The original board is embedded without modification. BrandArtwork renders the logo region `(134,150,680,710)` using a screen-composite alpha conversion: RGB values are retained in premultiplied space and alpha is max(R,G,B). Black becomes transparent with smooth edges while the approved design remains intact. The trial generated cutout was rejected and is not shipped.

packaging/Export-Icon.ps1 packages the approved icon region `(963,309,400,400)` as PNG-backed ICO frames at 16, 20, 24, 32, 40, 48, 64, 128 and 256 pixels. ArcHive.ico is shared by the executable resource, WPF window, MSI Installed Apps entry and Start Menu shortcut. The original icon's dark interior is retained. The executable/installer icon blocker is resolved; vector masters remain optional future work.

## Tokens

| Role | Color |
| --- | --- |
| Obsidian / window | #080D0B |
| Surface / cards | #14231B |
| Elevated / boundaries | #234532 |
| Phosphor / actions | #46E879 |
| Highlight / emphasis | #B3FFD0 |

See tokens.json and ArcHive.App/App.xaml. Segoe UI carries content; Consolas is reserved for branding text and small system labels. Green is an accent; body text remains pale neutral. Corners 7–12px, control padding 9–14px, shell spacing 24–28px. No scanline overlay.

## Layout and behavior

190px left navigation; flexible gallery; optional 360px inspector. Cards target at least 190px width and regroup when the content area changes. Gallery rows use a recycling VirtualizingStackPanel; only each realized row's cards are instantiated. The hidden selection model is collapsed, not a second rendered library. Image decoding uses two background workers, 640px previews and a bounded 64-entry memory cache; recycled/unloaded controls discard stale responses. Images over 25MB or unreadable files leave the fallback visible.

Library: local cover-rich cards, title and availability. Details: large artwork, title, primary launch/extract actions, editable metadata and expandable paths. Archive manager: a distinct compact archive-bearing game list with the same selected-game operations. It is not yet a batch queue or per-file preview browser. Empty results are explicit; no sample games are injected into real libraries.

## Remaining visual work

Cinematic full-width details, distinct cover/backdrop roles, contextual card actions, active navigation/selection treatment, transitions honoring reduced motion, and screen-reader/keyboard polish remain in the roadmap. Current responsive reflow is immediate, not animated. Native ComboBox/file picker controls retain platform styling. Validate minimum size 1000×650 and high DPI before release.
