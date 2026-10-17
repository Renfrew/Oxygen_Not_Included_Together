# Asset Provenance and Release Clearance

This document distinguishes inherited binary assets, newly supplied artwork, and translation catalogs. The repository-wide MIT license and historical attribution remain applicable to material inherited from the upstream project; this record does not claim ownership of third-party trademarks.

## Verified upstream baseline

- Upstream baseline: commit `1c9d7226c7f9dbde7c303a11af4adf5fd5c2ffbb` (2026-10-05), distributed under MIT as recorded in `LICENSE.md`.
- Upstream used the `ONI_Together/` paths. Files later moved to `ONI_United/` are compared by Git blob identity, not by assuming their path stayed unchanged.
- The inherited images and nine AssetBundle files listed below were verified against that baseline before this asset integration. The three external platform UI bundles are listed separately from the six embedded bundles.
- This historical finding does not cover newly supplied artwork or translation catalogs.

## Inherited assets embedded in the mod assembly

| Current path | Purpose | Upstream path at baseline | Status |
|---|---|---|---|
| `ONI_United/Assets/mod_mascot.png` | Loading/mascot art | `ONI_Together/Assets/mod_mascot.png` | Inherited; MIT baseline blob verified |
| `ONI_United/Assets/oni_together_link_chat.png` | Chat link graphic | `ONI_Together/Assets/oni_together_link_chat.png` | Inherited; MIT baseline blob verified |
| `ONI_United/Assets/discord.png` | Discord UI icon | `ONI_Together/Assets/discord.png` | Inherited; MIT baseline blob verified |
| `ONI_United/Assets/network_overlay_icon.png` | Network overlay graphic | `ONI_Together/Assets/network_overlay_icon.png` | Inherited; MIT baseline blob verified |
| `ONI_United/Assets/bundles/playercursor_{win,mac,lin}.bundle` | Player cursor assets | `ONI_Together/Assets/bundles/playercursor_{win,mac,lin}.bundle` | All three inherited; MIT baseline blobs verified |
| `ONI_United/Assets/bundles/networkindicators_{win,mac,lin}.bundle` | Network indicator assets | `ONI_Together/Assets/bundles/networkindicators_{win,mac,lin}.bundle` | All three inherited; MIT baseline blobs verified |

## Inherited external UI bundles

These files are packaged from `ModAssets` and loaded from the platform-specific external assets directory; they are not embedded in the mod assembly.

| Current path | Platform | Upstream path at baseline | Status |
|---|---|---|---|
| `ONI_United/ModAssets/assets/windows/oni_mp_ui_assets` | Windows | `ONI_Together/ModAssets/assets/windows/oni_mp_ui_assets` | Inherited; MIT baseline blob verified |
| `ONI_United/ModAssets/assets/mac/oni_mp_ui_assets` | macOS | `ONI_Together/ModAssets/assets/mac/oni_mp_ui_assets` | Inherited; MIT baseline blob verified |
| `ONI_United/ModAssets/assets/linux/oni_mp_ui_assets` | Linux | `ONI_Together/ModAssets/assets/linux/oni_mp_ui_assets` | Inherited; MIT baseline blob verified |

## Newly supplied ONI United artwork

| Current path | Intended use | Provenance and clearance |
|---|---|---|
| `ONI_United/Assets/ONI_United_Logo.png` | Main-menu logo; embedded resource identity `ONI_United.Assets.ONI_United_logo.png` | Supplied for ONI United. Artist/creator, copyright owner, original source, license, and permission terms have not been verified; user confirmation is required before release. |
| `ONI_United/Assets/syncing.png` | Synchronization marker; embedded resource identity `ONI_United.Assets.syncing.png` | Supplied for ONI United. Artist/creator, copyright owner, original source, license, and permission terms have not been verified; user confirmation is required before release. |

No ownership, license, permission, or third-party artwork attribution is inferred for these files.

## Translation catalogs

The following UTF-8 gettext catalogs are new localization text, not unchanged inherited binary assets:

- `ONI_United/ModAssets/translations/de.po` — German
- `ONI_United/ModAssets/translations/pl.po` — Polish
- `ONI_United/ModAssets/translations/zh.po` — Simplified Chinese
- `ONI_United/ModAssets/translations/ko.po` — Korean
- `ONI_United/ModAssets/translations/ru.po` — Russian

Their English source keys and text are maintained in `ONI_United/STRINGS.cs`. Catalogs must be reviewed as localization content; the upstream binary-asset comparison does not establish their provenance or quality.

## Removed legacy images

The following images are no longer present in the current asset tree:

| Former current path | Disposition |
|---|---|
| `ONI_United/Assets/oni_together_logo.png` | Superseded by `ONI_United/Assets/ONI_United_Logo.png` |
| `ONI_United/Assets/sync_icon.png` | Superseded by `ONI_United/Assets/syncing.png` |
| `ONI_United/Assets/fni_logo.png` | Removed as confirmed unused legacy artwork |
| `ONI_United/Assets/network_overlay_icon_old.png` | Removed as confirmed unused legacy artwork; `network_overlay_icon.png` remains |

Their historical upstream provenance does not change the fact that they are excluded from the current package.

## Other release-clearance items

Retain `LICENSE.md`, this provenance record, and `THIRD_PARTY_NOTICES.md` in distributions. Third-party software and copied-source provenance are documented separately in `THIRD_PARTY_NOTICES.md`; unresolved copied-source permissions remain a release gate. Game-provided assemblies and other proprietary game files are development references and must not be redistributed.

Before release, confirm for each newly supplied image:

1. creator or copyright owner;
2. original source;
3. applicable license or written permission;
4. whether it incorporates Klei, Discord, or another party's artwork or trademark;
5. modifications made by this project; and
6. which release artifact contains it.
