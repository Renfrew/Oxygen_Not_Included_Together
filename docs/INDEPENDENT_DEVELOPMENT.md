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
| Workshop identity | `3787172081` in `ONI_Together/Updater/Updater.cs` and the Workshop link in `README_NUGET.md` | Use the independent project's Workshop item; `0.8.0.5` remains associated with its historical release item |
| Mod/static identity | `Directory.Build.targets` emits `staticID: $(AssemblyName)` | Changed to `ONI_United`; existing enabled-mod lists may require users to re-enable the mod |
| Main assembly identity | `ONI_United` in `ONI_Together.csproj` | Changed with the generated static ID; code namespaces remain unchanged |
| Package identities | `MultiplayerONIUnited` and `ONI_United_API` in project files | Changed; the API bridge continues accepting the legacy `ONI_Together_API` assembly name |
| Namespaces and type names | `ONI_Together`, `ONI_Together_API`, and `Shared` across source | Retain; API reflection strings contain assembly-qualified inherited names |
| API reflection contract | Public namespaces remain `ONI_Together_API`; the runtime bridge accepts `ONI_United_API` and legacy `ONI_Together_API` assemblies | Preserve namespaces and legacy assembly recognition while consumers migrate |
| Configuration identity | PLib `[ConfigFile(SharedConfigLocation: true)]` on `ONI_Together.Configuration`; JSON property names are persisted | Retain class/namespace/assembly identity and property names until PLib path derivation and migration behavior are runtime-verified |
| Dedicated-server configuration | `oni_united_server_config/multiplayer_settings.json` | Changed; copy the legacy file on first load when the new path does not yet exist |
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

The public name is settled. Decisions remain for replacement logo/mascot/link art, asset and resource filenames, canonical repository and issue URLs, package/support/workflow destinations, Workshop title/description and item strategy, and whether a future public API package should adopt the new name.

## Migration work still required

1. Verify the `3787172081` Workshop item metadata and first `0.9.0` upload procedure before publishing.
2. Confirm how ONI and PLib derive mod/config identity from static ID, assembly, type, and distribution label at runtime.
3. Test upgrade behavior from `0.8.0.5` with an existing enabled-mod list, shared configuration, dedicated-server configuration, and representative saves.
4. Inventory serialized/reflected type consumers and third-party `ONI_Together_API` integrations; the bridge recognizes the legacy API assembly, but package consumers still need migration guidance.
5. Verify all active support and workflow destinations use the independent repository while preserving historical attribution links.
6. Replace or rebrand user-facing assets and translation content with documented provenance; do not silently rename compatibility keys.
7. Resolve the existing copied-source and historical proprietary-binary compliance items documented in `RELEASE_LEGAL.md` before a future release.

## Safest next development change

Validate an in-place `0.8.0.5` to `0.9.0` upgrade with the new assembly/static ID, migrated dedicated-server configuration, and legacy API-assembly recognition. Keep the inherited C# namespaces until a separately designed source- and binary-compatibility migration is ready.
