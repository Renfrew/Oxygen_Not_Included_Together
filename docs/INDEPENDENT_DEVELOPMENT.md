# Independent Development Boundary and Identifier Audit

**Audit date:** 2026-10-15
**Independent development version:** `0.9.0`
**Public name:** **Multiplayer: ONI United**

## Historical boundary

`0.8.0.5` is the final upstream-tracking release and the final baseline inherited from the upstream project's MIT-licensed history. The authoritative checkout was clean on `develop` at `c7e876174e0a28bd2d6bfd889a83405d07dc3220` before the `0.9.0` transition began. PR #63 was merged to the release line as `2384d2bc8da37a69965b9409d047acc65af33f3e`, whose second parent is `c7e8761`.

GitHub tag `v0.8.0.5` points directly to release merge `2384d2bc8da37a69965b9409d047acc65af33f3e`, establishing that commit as the tagged historical boundary. This verifies the tagged source boundary; it does not independently inspect the already-published Workshop binary.

The last imported upstream parent is `1c9d7226c7f9dbde7c303a11af4adf5fd5c2ffbb`, integrated through `88e7f06cb3c2f1d3cabdc389778bb8c083061016` while the inherited project was MIT-licensed. The MIT license and attribution remain in force for inherited material. Upstream changes after that verified baseline must not be copied or imported unless the specific material has separately verified compatible licensing or written permission.

## Identifier audit

| Area | Current definition | 0.9.0 treatment |
| --- | --- | --- |
| Development version | `ONI_Together/ONI_Together.csproj` `<Version>`; emitted to generated `mod_info.yaml` by `Directory.Build.targets` | Changed to `0.9.0` |
| User-facing mod title/description | `<ModName>` and `<ModDescription>` in `ONI_Together.csproj`; generated `mod.yaml`; UI strings, translations, README, images and rich presence | Use **Multiplayer: ONI United** |
| Workshop identity | `3787172081` in `ONI_Together/Updater/Updater.cs` | Use the independent project's Workshop item; `0.8.0.5` remains associated with its historical release item |
| Mod/static identity | `Directory.Build.targets` emits `staticID: $(AssemblyName)` | Changed to `ONI_United`; existing enabled-mod lists may require users to re-enable the mod |
| Main assembly identity | `ONI_United` in `ONI_Together.csproj` | Changed with the generated static ID; code namespaces remain unchanged |
| Package identity | `MultiplayerONIUnited` in the main project | Changed for the independent line; the former external API package is discontinued and removed |
| Namespaces and type names | `ONI_Together` and `Shared` across the retained source | Retain; namespace renaming is outside this transition |
| External mod API | Former `ONI_Together_API` / `ONI_United_API` project and reflection bridge | Permanently discontinued and removed in `0.9.0` |
| Configuration identity | PLib `[ConfigFile(SharedConfigLocation: true)]` on `ONI_Together.Configuration`; JSON property names are persisted | Retain class/namespace/assembly identity and property names until PLib path derivation and migration behavior are runtime-verified |
| Dedicated server | Former standalone dedicated-server project and configuration | Permanently discontinued and removed in `0.9.0` |
| Save compatibility | ONI save paths remain game-owned; transferred saves preserve active-mod labels/default static IDs; multiplayer sync components use inherited serialized type identity, with many runtime-only components marked `SkipSaveFileSerialization` | The static ID changes to `ONI_United`; retain namespaces and serialized type names, then verify upgrade behavior with representative saves and enabled-mod lists |
| Local diagnostics/development paths | `ONI_United_Log.txt`, `ONI_United`, and local build-folder settings | Changed for the independent line |
| Repository and support links | Active project links use `Renfrew/Oxygen_Not_Included_Together`; historical upstream links remain for attribution | Use the independent repository for active project and package links |
| Assets and translations | Inherited `oni_together_*` assets, embedded-resource names, translation contexts and user-facing strings | Update visible text now; retain inherited resource/key names until replacement assets and compatibility review are complete |

## Safe immediate changes

- Set the development version to `0.9.0`.
- Record the independent-development and licensing boundary.
- Preserve the MIT license, upstream attribution, third-party notices, and asset provenance.
- Use **Multiplayer: ONI United** for public display text and `ONI_United` for new assembly/static identity while retaining code namespaces and explicit compatibility paths.

## Remaining branding decisions

The public name is settled. Decisions remain for replacement logo/mascot/link art, asset and resource filenames, canonical issue/support destinations, and Workshop title/description.

## Migration work still required

1. Verify the `3787172081` Workshop item metadata and first `0.9.0` upload procedure before publishing.
2. Confirm how ONI and PLib derive mod/config identity from static ID, assembly, type, and distribution label at runtime.
3. Test upgrade behavior from `0.8.0.5` with an existing enabled-mod list, shared configuration, and representative saves.
4. Document the removal of the external API and dedicated server in release notes so integrators and operators are not given a false compatibility expectation.
5. Verify all active support and workflow destinations use the independent repository while preserving historical attribution links.
6. Replace or rebrand user-facing assets and translation content with documented provenance; do not silently rename compatibility keys.
7. Resolve the existing copied-source and historical proprietary-binary compliance items documented in `RELEASE_LEGAL.md` before a future release.

## Safest next development change

Validate an in-place `0.8.0.5` to `0.9.0` upgrade with the new assembly/static ID. Keep the inherited C# namespaces and the main mod's internal OxySync implementation; do not restore the discontinued external API bridge or dedicated-server application.
