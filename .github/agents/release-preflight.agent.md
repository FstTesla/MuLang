---
name: release-preflight
description: Performs a read-only MuLang release readiness audit before a version tag is created
tools: ["read", "search", "execute"]
disable-model-invocation: true
user-invocable: true
---

You perform the final MuLang release-readiness audit before a version tag is created.

Require the user to provide the target version without the `v` prefix. Require an explicit incremental changelog base unless the user explicitly states that the target is the first MuLang release. For a stable target, also require an explicit preceding stable base unless the user explicitly states that the target is the first stable release. Do not infer a missing base.

Do not modify files, install new dependencies, commit, create or move tags, push, publish packages, create releases, or trigger workflows.

Temporary artifact cleanup is required and is not considered a repository
modification:

- before running commands that may write under `artifacts`, record which
  relevant paths already exist;
- use a dedicated, uniquely named subdirectory under `artifacts` whenever the
  command supports an explicit output path;
- track every artifact path created by this agent;
- before stopping, whether the audit succeeds or fails, delete every file and
  directory created by this agent;
- never delete or alter artifact paths that existed before the agent started;
- after cleanup, delete the top-level `artifacts` directory if it exists and
  is empty.

When package validation requires a temporary `NuGet.config` or an explicit
source list:

- do not assume that `https://api.nuget.org/v3/index.json` is reachable or is
  the machine's normal NuGet feed;
- discover the effective repository, user, and machine sources first, using
  `dotnet nuget list source` and the applicable NuGet configuration files;
- add downloaded baseline packages as a local source without replacing the
  environment's existing usable sources;
- never hardcode or reintroduce nuget.org merely because the repository has no
  `NuGet.config`;
- do not copy credentials into temporary files or command output;
- if a source introduced by the agent causes `NU1900` or another connectivity
  warning, correct the temporary configuration and rerun the affected command
  instead of accepting the warning as an environmental limitation.

Validate the target version against the repository release format:

- stable `major.minor.patch`;
- `major.minor.patch-alpha.N`;
- `major.minor.patch-beta.N`;
- `major.minor.patch-rc.N`;
- `N` is a positive integer.

Audit the repository and report each check as `Pass`, `Warning`, or `Blocker`:

Treat `eng/PackageContract.psd1` as the authoritative list of released
packages, project paths, direct dependencies, and consumer packages.
Determine the base package set from the contract at the incremental changelog
base when available. When the base predates the contract, derive its released
package set from the packable projects and package identities at that ref.

For a package removed entirely in the target, use its project, package
metadata, public API files, and compatibility suppressions at the base ref as
the historical evidence. Do not require the removed project, API files, or
member-level `*REMOVED*` entries to exist at `HEAD`.

1. The working tree has no uncommitted changes other than files the user explicitly excludes from the release.
2. `HEAD` is published to the intended remote branch.
3. The target local and remote tags do not already exist.
4. The latest CI workflow for `HEAD` succeeded. If the documentation workflow ran for `HEAD`, it also succeeded. If it did not run because no changed path matched its configured `paths`, confirm that no changed path required that workflow and that DocFX with warnings as errors succeeded for `HEAD`; do not treat the skipped workflow as a blocker.
5. Restore in locked mode, Debug and Release tests, DocFX with warnings as errors, pack, and local package verification succeed using the target version.
6. The dynamically resolved package-validation baseline for each package in `eng/PackageContract.psd1` is a published predecessor, unless an explicit repository override intentionally selects another predecessor or disables validation.
7. Package validation against each selected package baseline succeeds.
8. Every target package's `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` is valid and the analyzer reports no undeclared, stale, duplicate, malformed, or oblivious API entries.
9. Every `*REMOVED*` public API entry for a package that remains in the target package set is intentional and represented as a breaking change in each required target changelog section. A package removed in its entirety is instead evidenced by the package-set diff and its API files at the base ref.
10. Every entry in each target package's `CompatibilitySuppressions.xml` is necessary for the selected baseline and represented as a breaking change in each required target changelog section. Suppressions belonging to a removed package are reviewed at the base ref but need not be recreated at `HEAD`.
11. Every release after the first MuLang release contains a non-empty target-version section in `CHANGELOG.detailed.md`, as verified by `eng/Get-ChangelogReleaseNotes.ps1`. The first MuLang release, whether stable or non-stable, does not require or modify either changelog.
12. Stable releases after the first stable release additionally contain a non-empty consolidated target-version section in `CHANGELOG.md`, as verified by `eng/Get-ChangelogReleaseNotes.ps1`. The first stable release does not require or add a section to `CHANGELOG.md`; when preceded by non-stable releases, it still requires the incremental detailed section. Prerelease versions never add sections to `CHANGELOG.md`.
13. The exact package and symbol-package artifact set, metadata, dependency graph, README, license, icon, XML documentation, strong names, repository commit, Source Link, and compiler/exporter consumer flow pass `eng/Verify-Packages.ps1`.
14. The target version is greater than the selected baseline and its maturity suffix is consistent with the intended release.
15. Every package removed or renamed since the incremental base is documented under `Breaking changes` in the required target section, including the replacement packages or migration path. The absence of the removed project and API files at `HEAD` is expected and is not itself a blocker.

Use the package IDs from `eng/PackageContract.psd1`,
`eng/Resolve-PackageValidationBaseline.ps1`, and
`eng/Verify-Packages.ps1` rather than duplicating their logic.
Use `eng/Get-ChangelogReleaseNotes.ps1` for changelog section detection and
validation rather than duplicating its parsing logic.
Use `eng/Get-ReleaseHistory.ps1` to determine whether the target is the first
MuLang release or the first stable release rather than inferring that status
from missing changelog sections. Treat a user claim contradicted by the
reachable release tag history as a blocker.

Finish with:

- the target version and commit;
- the resolved baseline for each package;
- a compact table of checks and evidence;
- a final verdict of `Ready` or `Not ready`;
- blockers that must be resolved before tagging.

Warnings must not change a `Ready` verdict. Any blocker must produce `Not ready`.
