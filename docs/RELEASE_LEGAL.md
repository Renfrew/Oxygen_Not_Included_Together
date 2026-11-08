# Release Legal and License Boundary

**Audit date:** 2026-10-15  
**Release line:** Published 0.8.0.5 boundary; independent development begins with 0.9.0
**Status:** Historical boundary verified; inherited non-code assets verified; remaining compliance gates below

## Final upstream-tracking release

PR #63 / `0.8.0.5` is the final release that tracks the original upstream repository. The verified release ancestry is:

- release-line merge: `2384d2bc8da37a69965b9409d047acc65af33f3e`
- `develop` version commit and second parent of the release merge: `c7e876174e0a28bd2d6bfd889a83405d07dc3220`
- last imported upstream parent: `1c9d7226c7f9dbde7c303a11af4adf5fd5c2ffbb`
- upstream integration merge: `88e7f06cb3c2f1d3cabdc389778bb8c083061016`, dated 2026-10-05

GitHub tag `v0.8.0.5` points directly to release merge `2384d2bc8da37a69965b9409d047acc65af33f3e`, establishing that commit as the tagged historical boundary. This verifies the tagged source boundary; it does not independently inspect the already-published Workshop binary.

The imported upstream parent and the downstream parent both contained the MIT License. Upstream changed to a restrictive source-available license on 2026-10-11. That restrictive-license commit is not an ancestor of the audited `develop` branch.

Version `0.9.0` begins independent development. Code published upstream after the verified MIT baseline must not be incorporated automatically. Each proposed import requires compatible license evidence or written permission. Inherited material continues to carry the retained MIT license and attribution.

## Current compliance gates

- Preserve `LICENSE.md` and upstream attribution.
- Treat `docs/INDEPENDENT_DEVELOPMENT.md` as the identifier and migration boundary for the independent line.
- Include `THIRD_PARTY_NOTICES.md` with every binary release.
- Preserve `ASSET_PROVENANCE.md` with the release. Every currently tracked image and asset bundle has been verified as identical to the last imported MIT upstream snapshot; re-review any asset added or modified after that baseline.
- Document permission for copied FUI helpers and gist-derived code, or replace them.
- Confirm the final package contains no ONI, Unity, publicized game, Steamworks.NET, or Steam SDK binaries.
- Address historical committed proprietary binaries through a separately approved history-remediation plan.
- Run a historical secret scan before rewriting public history.
- Build, inspect, and runtime-test the exact final commit and artifact.

Passing the technical test gate does not satisfy the legal/compliance gate, and passing this source audit does not verify a future package whose contents have not been inspected.
