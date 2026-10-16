# Asset Provenance and Release Clearance

This file records the release-clearance basis for non-code assets. The repository-wide MIT license is not limited to source code: it grants rights in the Software and associated documentation files. Assets demonstrably inherited unchanged from the upstream repository while that repository was distributed under MIT are therefore treated as covered by that upstream MIT grant, without requiring a separate author-by-author investigation.

## Verified upstream baseline

- Last imported upstream commit: `1c9d7226c7f9dbde7c303a11af4adf5fd5c2ffbb` (2026-10-05).
- License at that imported commit: MIT, as recorded in `LICENSE.md`.
- Verification method: every tracked image and asset bundle listed below exists at the same path in that commit and has the same Git blob object ID in current `develop`.
- Result: all currently tracked non-code assets are unchanged files inherited from the MIT-licensed upstream baseline.

This is a downstream-license finding, not a claim that this project created the assets or owns third-party trademarks. The release must retain `LICENSE.md`, attribution, and the unofficial-community-project disclaimer. Any future asset added or changed after this baseline requires a new provenance review.

## Embedded in the mod assembly

| Asset | Purpose | Verified source | Clearance |
|---|---|---|---|
| `ONI_Together/Assets/mod_mascot.png` | Loading/mascot art | Identical to upstream MIT commit `1c9d722...` | **MIT-COVERED (INHERITED)** |
| `ONI_Together/Assets/oni_together_logo.png` | Project logo | Identical to upstream MIT commit `1c9d722...` | **MIT-COVERED (INHERITED)** |
| `ONI_Together/Assets/oni_together_link_chat.png` | UI link graphic | Identical to upstream MIT commit `1c9d722...` | **MIT-COVERED (INHERITED)** |
| `ONI_Together/Assets/discord.png` | Discord UI icon | Identical to upstream MIT commit `1c9d722...` | **MIT-COVERED (INHERITED)** |
| `ONI_Together/Assets/network_overlay_icon.png` | Network overlay icon | Identical to upstream MIT commit `1c9d722...` | **MIT-COVERED (INHERITED)** |
| `ONI_Together/Assets/sync_icon.png` | Synchronization icon | Identical to upstream MIT commit `1c9d722...` | **MIT-COVERED (INHERITED)** |
| `ONI_Together/Assets/bundles/playercursor_{win,mac,lin}.bundle` | Player cursor assets | All three bundles identical to upstream MIT commit `1c9d722...` | **MIT-COVERED (INHERITED)** |
| `ONI_Together/Assets/bundles/networkindicators_{win,mac,lin}.bundle` | Network indicator assets | All three bundles identical to upstream MIT commit `1c9d722...` | **MIT-COVERED (INHERITED)** |

## Tracked but not currently embedded by the main project

| Asset | Purpose | Verified source | Clearance |
|---|---|---|---|
| `ONI_Together/Assets/fni_logo.png` | Experimental/legacy logo | Identical to upstream MIT commit `1c9d722...` | **MIT-COVERED (INHERITED)** |
| `ONI_Together/Assets/network_overlay_icon_old.png` | Legacy overlay icon | Identical to upstream MIT commit `1c9d722...` | **MIT-COVERED (INHERITED)** |

## Future independent releases

For every asset first added or modified after the final upstream-tracking release, record:

1. creator or copyright owner;
2. original source URL or source file;
3. applicable license or written permission;
4. whether it incorporates Klei, Discord, or another party's artwork or trademark;
5. modifications made by this project;
6. which release artifact contains it.

Do not automatically extend this inherited-assets conclusion to future additions, locally modified assets, or files imported from post-MIT upstream revisions.
