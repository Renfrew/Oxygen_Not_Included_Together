![Logo](https://i.imgur.com/GCIbhpn.png)

# Multiplayer: ONI United

**Multiplayer: ONI United** is an independent, community-maintained multiplayer mod for _Oxygen Not Included_, built from the MIT-licensed history of **Oxygen Not Included Together**.

This fork focuses on updating the original project, fixing multiplayer synchronization issues, improving stability, and maintaining compatibility with current versions of the game.

> **Development Status:** Work in progress / experimental.
>
> Multiplayer support is still under active development. Expect bugs, desynchronization issues, incomplete features, and breaking changes between versions.

---

## About This Fork

This repository is based on **Oxygen Not Included Together**, originally created by **Luke Rapkin** and contributors.

The original project introduced multiplayer functionality to _Oxygen Not Included_ using a custom networking and synchronization system.

This fork continues that work with additional:

- Game compatibility updates
- Multiplayer synchronization fixes
- Client/host simulation improvements
- Entity and building synchronization fixes
- Animation and reaction synchronization improvements
- Game-speed synchronization improvements
- Stability and crash fixes
- Networking and lifecycle fixes
- General maintenance and refactoring

The project remains experimental and is not yet intended to provide a completely synchronized multiplayer experience.

---

## Attribution

Original project:

**Oxygen Not Included Together**  
Created by **Luke Rapkin** and contributors.

Original repository:

https://github.com/Renfrew/Oxygen_Not_Included_Together

This community fork is maintained by **Liang Chen** and contains additional fixes, compatibility updates, and multiplayer development.

This project is an unofficial community project and is **not affiliated with, endorsed by, or supported by Klei Entertainment**.

### Release Lineage and License Boundary

The published `0.8.0.5` release represented by PR #63 is the final release that tracks and integrates changes from the original upstream repository. The release-line merge is `2384d2bc8da37a69965b9409d047acc65af33f3e`; its `develop` parent containing the `0.8.0.5` version change is `c7e876174e0a28bd2d6bfd889a83405d07dc3220`. GitHub tag `v0.8.0.5` points to the release merge, establishing `2384d2bc8da37a69965b9409d047acc65af33f3e` as the published release boundary.

The last upstream code imported into this release line is upstream commit `1c9d7226c7f9dbde7c303a11af4adf5fd5c2ffbb`, merged on October 5, 2026 while that code was published under the MIT License. The upstream license was changed later, on October 11, 2026. Those later source-available license commits are not part of this release's Git ancestry.

Development version `0.9.0` begins the independent project line. Upstream changes made after the verified MIT baseline will not be incorporated unless the specific material has separately verified compatible licensing or written permission. Inherited material remains under the retained MIT License and attribution.

The independent project is publicly named **Multiplayer: ONI United**. Its package, assembly, static, configuration, development-folder, repository, and Workshop identities have moved to the independent project line. The inherited C# namespaces remain unchanged for now. The former external mod-integration API and dedicated-server application are not supported in `0.9.0`.

See [`LICENSE.md`](LICENSE.md), [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md), and [`ASSET_PROVENANCE.md`](ASSET_PROVENANCE.md) for the release's license, bundled-component notices, and asset review status.
See [`docs/INDEPENDENT_DEVELOPMENT.md`](docs/INDEPENDENT_DEVELOPMENT.md) for the `0.9.0` identifier audit and migration boundary.

---

## Multiplayer Demo

![Multiplayer Demo](https://i.ibb.co/G136FH2/download.jpg)

---

## Current Development

Development currently focuses on improving synchronization between the host and connected clients.

Major areas under development include:

- Building state synchronization
- Power and operational state synchronization
- Duplicant animation and reaction synchronization
- Chore and interaction synchronization
- Game-speed synchronization
- Entity lifecycle synchronization
- Save/load and reconnect behavior
- Host-authoritative simulation
- Client-side simulation suppression
- Crash and desynchronization fixes

Because _Oxygen Not Included_ was designed as a single-player simulation game, multiplayer requires synchronizing systems that were never designed to run across multiple game instances.

As a result, some systems may behave differently between the host and clients while development continues.

---

## Reporting Bugs

If you encounter a problem, please open an issue in this repository.

When possible, include:

- Host log
- Client log
- Steps to reproduce the issue
- Whether the problem occurs consistently
- Screenshots or video
- Save file, if relevant
- Mod version / commit
- Oxygen Not Included version

Detailed reproduction information makes synchronization bugs significantly easier to investigate.

---

## Installation

> Installation instructions for normal players will be added or updated as the fork approaches a stable Workshop release.

For development builds, the compiled mod can be installed in the normal Oxygen Not Included development mod directory.

Typical Windows location:

```text
%USERPROFILE%\Documents\Klei\OxygenNotIncluded\mods\dev
```

---

# Development Setup

## Requirements

You will need:

- Visual Studio with C#/.NET development support
- .NET Framework 4.7.2
- A local installation of Oxygen Not Included
- .NET SDK 10.0.401 (configured via `global.json`)
- Git

Visual Studio installations used with this repository must support .NET SDK `10.0.401`. Verify SDK support in Visual Studio Installer for your installed edition/workloads.

---

## 1. Clone the Repository

```bash
git clone https://github.com/Renfrew/Oxygen_Not_Included_Together.git
cd Oxygen_Not_Included_Together
```

---

## 2. Open the Solution

Open `ONI_United.sln` in Visual Studio, or open the repository folder in VS Code.

The repository uses `global.json` at the solution root to select .NET SDK `10.0.401` with patch roll-forward.

The repository also includes a minimal root `.editorconfig` for shared baseline editor behavior (encoding, line endings, and newline handling). Keep personal editor preferences in local user/editor settings.

If you use VS Code, `.vscode/extensions.json` includes an optional C# extension recommendation. It is not required to build the project.

---

## 3. Configure Local Paths

Copy:

```text
Directory.Build.props.default
```

to:

```text
Directory.Build.props.user
```

`Directory.Build.props.user` contains machine-specific paths and should not be committed.

`Directory.Build.props.user` is intentionally local and ignored by Git. The build imports it conditionally when present.

Configure `GameLibsFolder` to point to your local Oxygen Not Included managed assemblies.

Example:

```xml
<GameLibsFolder>C:\Program Files (x86)\Steam\steamapps\common\OxygenNotIncluded\OxygenNotIncluded_Data\Managed</GameLibsFolder>
```

Configure `ModFolder` to point to your Oxygen Not Included development mod directory.

Example:

```xml
<ModFolder>C:\Users\YourName\Documents\Klei\OxygenNotIncluded\mods\dev</ModFolder>
```

---

## 4. Restore .NET Tools

Run:

```bash
dotnet tool restore
```

> macOS SSH note  
> In non-interactive SSH sessions, `dotnet` may not be on `PATH` even when installed at `/usr/local/share/dotnet/dotnet`.  
> Prefer standard shell initialization so SSH sessions include .NET on `PATH`, or invoke that documented executable path explicitly in automation.

The project uses an assembly publicizer so internal Oxygen Not Included game APIs can be referenced during development.

### Linux

Linux development may require .NET 6.0 for the publicizer.

See the original setup documentation:

https://github.com/Renfrew/Oxygen_Not_Included_Together/wiki/Setting-up-publiciser-requirement-on-Linux

---

## 5. Restore NuGet Packages

Visual Studio will normally restore packages automatically.

They can also be restored manually if necessary.

For CLI/VS Code workflows, run:

```bash
dotnet restore ONI_United.sln
```

---

## 6. Build

Build the solution from Visual Studio.

For CLI/VS Code workflows, run:

```bash
dotnet build ONI_United.sln
```

The first build creates the publicized Oxygen Not Included reference assemblies.

Because these references are generated during the build, Visual Studio may temporarily report unresolved references after the first build.

If this occurs:

1. Build the project once.
2. Restart Visual Studio.
3. Reopen the solution.
4. Build again.

---

# Debugging Oxygen Not Included

The game can be configured to allow a managed debugger such as Visual Studio to attach directly to the Unity process.

> **Warning**
>
> The following process replaces game binaries with Unity development-player binaries.
> Back up the original files first.
>
> Steam's **Verify integrity of game files** feature can restore the original files if necessary.

---

## 1. Determine the Unity Version

Open:

```text
%USERPROFILE%\AppData\LocalLow\Klei\Oxygen Not Included\Player.log
```

The Unity version used by the game appears near the beginning of the log.

Example:

```text
Unity 2022.3.62f2
```

---

## 2. Install the Matching Unity Version

Download the exact Unity version from:

https://unity.com/releases/editor/archive

Locate:

```text
Editor\Data\PlaybackEngines\windowsstandalonesupport\Variations\win64_development_mono\
```

Copy the required development-player files into the Oxygen Not Included installation directory.

Typically:

```text
WindowsPlayer.exe
UnityPlayer.dll
WinPixEventRuntime.dll
```

Rename:

```text
WindowsPlayer.exe
```

to:

```text
OxygenNotIncluded.exe
```

and replace the corresponding game binaries.

---

## 3. Enable Managed Debugging

Open:

```text
OxygenNotIncluded_Data\boot.config
```

and add:

```text
wait-for-managed-debugger=1
player-connection-debug=1
```

---

## 4. Attach Visual Studio

Install **Visual Studio Tools for Unity**.

Launch Oxygen Not Included through Steam.

The development player should pause while waiting for a debugger.

In Visual Studio:

```text
Debug
→ Attach Unity Debugger
→ Select Oxygen Not Included
```

The game will continue after the debugger connects.

---

# Architecture

The multiplayer implementation uses a host-authoritative architecture.

In general:

```text
Host
 ├─ Runs authoritative game simulation
 ├─ Processes game state
 ├─ Sends synchronized state
 │
 ▼
Network Layer
 │
 ▼
Clients
 ├─ Receive authoritative state
 ├─ Apply synchronized state
 ├─ Reproduce visual/gameplay behavior
 └─ Avoid running conflicting simulation where necessary
```

A major development challenge is identifying which Oxygen Not Included systems must run normally on clients and which systems must instead be driven by synchronized host state.

---

# Contributing

Contributions are welcome.

Bug fixes, compatibility improvements, synchronization improvements, documentation, and testing are all useful.

Before opening a pull request:

- Keep changes focused where possible.
- Explain the problem being solved.
- Explain any synchronization or lifecycle assumptions.
- Include relevant testing information.
- Avoid unrelated formatting changes.
- Document unusual Harmony patches or game-specific workarounds.

For synchronization changes, testing with both a host and at least one client is strongly recommended.

---

## AI-Assisted Contributions

AI-assisted development is allowed.

AI tools can be useful for:

- Code analysis
- Debugging
- Documentation
- Refactoring suggestions
- Understanding decompiled game code

However, contributors remain responsible for understanding and testing submitted code.

Pull requests consisting primarily of unreviewed or untested generated code may be rejected.

AI should be treated as a development tool, not as a substitute for understanding how the code works.

---

# License

This project is distributed under the **MIT License**.

The original Oxygen Not Included Together project is:

```text
Copyright (c) 2025 - 2026 Luke Rapkin
```

Additional modifications in this fork include work by:

```text
Copyright (c) 2026 Liang Chen
```

See [`LICENSE.md`](LICENSE.md) for the complete license text.

---

# Disclaimer

_Oxygen Not Included_ is developed by **Klei Entertainment**.

This project is an unofficial community-created modification and is not affiliated with, endorsed by, sponsored by, or supported by Klei Entertainment.

All Oxygen Not Included trademarks, game assets, and related intellectual property belong to their respective owners.
