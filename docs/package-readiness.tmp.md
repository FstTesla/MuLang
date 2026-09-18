# Package Readiness Plan

## Goal

Prepare `MuLang` for an initial public release to NuGet.org as a single package, with complete public API documentation, deterministic tag-derived versioning, package metadata, source debugging support, automated API compatibility checks, and repeatable package validation.

The initial release target is `0.1.0-alpha.1`. Release versions are derived from Git tags using the format `v<major>.<minor>.<patch>` with an optional `alpha.N`, `beta.N`, or `rc.N` suffix, where `N` is a positive integer.

## Decisions

| Concern | Decision |
|---|---|
| Distribution | Public package on NuGet.org |
| Package ID | `MuLang` |
| Authors | `FstTesla` |
| Copyright | `Copyright (c) 2026 Filippo Mineo` |
| Package tags | `compiler;dsl;embedded-language;expression-language;static-typing;type-checking;dotnet;micro-language` |
| License | Apache-2.0 |
| Repository and project URL | `https://github.com/FstTesla/MuLang` |
| Initial version | `0.1.0-alpha.1` |
| Version source | GitHub Actions tag workflow |
| Tag format | <code>v&lt;major&gt;.&lt;minor&gt;.&lt;patch&gt;[-(alpha&#124;beta&#124;rc).N]</code>, with `N >= 1` |
| Release workflow | `.github\workflows\publish.yml` |
| NuGet.org user | `FstTesla` |
| GitHub Environment | None |
| Duplicate publication | Fail |
| Temporary publication target | GitHub Packages under the `FstTesla` namespace |
| Temporary authentication | Workflow-scoped `GITHUB_TOKEN` |
| Temporary package visibility | Private |
| Package structure | One package for the initial releases |
| API compatibility | Automatically enforced |
| Assembly signing | Strong-name sign every assembly with the root `MuLang.snk` key |
| Symbols and sources | Publish a symbol package with Source Link |
| Package icon | `MuLang.png`, generated from `MuLang.svg` |
| Consumer examples | `docs\public\examples.md` |
| Release maturity | `alpha` is experimental, `beta` is prerelease, and `rc` is preview |
| Changelog source | Reviewed diff from an explicitly specified base tag or commit |
| Changelog preparation | Use `.github\agents\release-changelog.agent.md`, then review and commit `CHANGELOG.md` before tagging |
| Changelog categories | `Breaking changes`, `New features`, and `Fixes` |
| Release readiness | Use `.github\agents\release-preflight.agent.md` before tagging |
| Post-release API governance | Use `.github\agents\post-release-finalization.agent.md` after successful publication |
| GitHub Release policy | Create releases for beta, RC, and stable versions; exclude alpha versions |

## 1. Establish Package Identity and Legal Metadata

1. Add an Apache-2.0 `LICENSE` file at the repository root.
2. Centralize repository-wide package metadata where it can be reused without applying package-only settings to test projects.
3. Configure the production project with:
   - package ID;
   - title;
   - concise description;
   - authors;
   - license expression;
   - project URL;
   - repository URL and repository type;
   - package tags;
   - package icon;
   - README;
   - copyright;
   - package release notes source.
4. Set `PackageRequireLicenseAcceptance` to `false`, consistently with common Apache-2.0 package distribution.
5. Configure `MuLang.png` as the package icon and include it at the package root.
6. Retain `MuLang.svg` as the editable vector source without including it in the package.
7. Ensure the README and license are included at the package root.

Completion criteria:

- `dotnet pack` emits no package metadata warnings.
- The generated manifest contains all decided metadata.
- NuGet clients display the README, license, and icon correctly.

## 2. Introduce Tag-Derived Versioning

1. Do not add a Git-derived versioning package such as MinVer.
2. Create official versioned packages only in the GitHub Actions release workflow.
3. Trigger the workflow only for Git tags whose names begin with `v`.
4. Remove exactly the initial `v` from `github.ref_name` and use the remaining suffix as the package `Version`.
5. Validate the version before restore, build, test, or pack proceeds. It MUST be stable SemVer or use exactly one of the `alpha.N`, `beta.N`, or `rc.N` suffixes, where `N` is a positive integer.
6. Accept stable and prerelease versions, including:
   - `v0.1.0-alpha.1`, producing version `0.1.0-alpha.1`;
   - `v0.1.0-beta.1`, producing version `0.1.0-beta.1`;
   - `v0.1.0-rc.1`, producing version `0.1.0-rc.1`;
   - `v0.1.0`, producing version `0.1.0`;
   - `v1.0.0`, producing version `1.0.0`.
7. Reject tags that begin with `v` but do not contain an accepted complete SemVer version, including incomplete or textual values.
8. Pass the derived value to `dotnet pack` through the `Version` MSBuild property so package and assembly version metadata originate from the same release version.
9. Do not produce officially versioned release packages from branch or pull-request workflows.
10. Document the release-tag convention and prohibit independent manual version overrides in the release workflow.

Completion criteria:

- A build of `v0.1.0-alpha.1` produces `MuLang.0.1.0-alpha.1.nupkg`.
- Stable and prerelease tags produce the corresponding package and assembly metadata.
- Invalid `v` tags fail before package creation.
- Untagged commits cannot accidentally produce a stable release version.

## 3. Document the Public API

1. Inventory the complete public surface of the production assembly, including:
   - compiler entry points and compilation results;
   - diagnostics and source text types;
   - type-system APIs;
   - environment and symbol construction APIs;
   - language profiles and feature settings;
   - .NET runtime context, delegates, adapters, and exceptions.
2. Review whether every public type and member is intentionally public before documenting it.
3. Reduce unintended public exposure before establishing the compatibility baseline.
4. Add XML documentation to every intentional public type and member.
5. Include behavioral contracts that consumers need to use the API safely:
   - nullability;
   - ownership and immutability;
   - accepted runtime representations;
   - thrown exceptions;
   - environment/profile compatibility;
   - budget, cancellation, and recursion behavior.
6. Enable XML documentation file generation for the production project.
7. Promote missing public XML documentation diagnostics to errors after the initial documentation pass is complete.
8. Include the generated XML documentation file beside `MuLang.dll` in the package.

Completion criteria:

- Every intentional public symbol has useful XML documentation.
- Missing documentation fails the production build.
- The package contains `lib\net10.0\MuLang.dll` and `lib\net10.0\MuLang.xml`.

## 4. Establish API Compatibility Control

1. Use `Microsoft.CodeAnalysis.PublicApiAnalyzers` as a private development dependency with a floating major version.
2. Keep the API shipped in `0.1.0-alpha.1` in `PublicAPI.Shipped.txt`.
3. Record additions and intentional removals since the baseline in `PublicAPI.Unshipped.txt`.
4. Treat missing, removed, malformed, duplicate, or oblivious public API declarations as build errors.
5. Enable .NET SDK package validation for every package build.
6. During the release workflow, query private GitHub Packages and select the highest published supported version that precedes the version being packed.
7. Exclude the current version so rerunning a previously published tag retains the preceding baseline.
8. Allow the repository variable `PACKAGE_VALIDATION_BASELINE_VERSION` to select an explicit published predecessor or use `none` to disable the baseline for an exceptional release.
9. Keep intentional breaking changes in the reviewed `CompatibilitySuppressions.xml` file.
10. Define the prerelease compatibility policy:
   - compatibility breaks are permitted before `1.0.0`;
   - they must still be explicit in the API baseline and release notes;
   - accidental breaks must fail validation;
   - stable releases follow SemVer compatibility requirements.
11. Consolidate shipped/unshipped API files after each selected release baseline.

Completion criteria:

- Unreviewed public API changes fail the build.
- Package validation detects binary or source compatibility regressions against the configured baseline.
- Intentional prerelease breaks remain visible in version control and release notes.

## 5. Configure Assembly Signing, Symbols, Determinism, and Source Link

1. Preserve strong-name signing for every project through `Directory.Build.props` and the root `MuLang.snk` key.
2. Keep signed friend-assembly declarations tied to the full public key rather than only the assembly name.
3. Preserve deterministic and continuous-integration build settings already defined in `Directory.Build.props`.
4. Configure repository metadata required by Source Link.
5. Confirm whether the .NET 10 SDK provides the required GitHub Source Link integration without an additional package; add a private Source Link dependency only if validation proves it necessary.
6. Produce portable PDB files.
7. Generate a `.snupkg` symbol package.
8. Include source revision information in assembly informational metadata.
9. Verify that published PDB documents resolve to immutable commit-specific GitHub URLs.
10. Verify source debugging from a clean consumer project using only the `.nupkg` and `.snupkg`.

Completion criteria:

- Every produced assembly has the expected public key token.
- `InternalsVisibleTo` remains valid for the signed test assembly.
- Packing produces both `.nupkg` and `.snupkg`.
- The symbol package is accepted by NuGet.org validation.
- A debugger can retrieve the matching source for a packaged assembly.

## 6. Improve Consumer Documentation

1. Use the root README as the concise repository and package landing page.
2. Document:
   - package purpose and current prerelease status;
   - supported target framework;
   - installation;
   - the compiler/environment/runtime workflow;
   - links to the normative language specification;
   - compatibility and versioning expectations;
   - license and repository links.
3. Keep detailed language semantics under `docs\public\language` and avoid duplicating normative content in the README.
4. Keep complete C# usage examples in `docs\public\examples.md` and link them from the README.
5. Prepare release notes by invoking the repository release-changelog agent with an explicit base tag or commit and the target version.
6. Have the agent inspect the complete diff rather than relying only on commit subjects.
7. Classify consumer-visible changes as `Breaking changes`, `New features`, or `Fixes`, omitting empty categories and internal-only work.
8. Review and commit the generated `CHANGELOG.md` section before creating the version tag.
9. Run the read-only release-preflight agent against the target version and resolve every blocker before tagging.
10. Require beta, RC, and stable tagged commits to contain the matching changelog section. Alpha tags may omit it.
11. Create a GitHub Release after package publication for beta, RC, and stable versions, using the changelog section as its notes. Beta and RC versions are marked as prereleases.
12. For alpha packages, link `PackageReleaseNotes` to the tagged changelog only when a matching section exists; otherwise omit the metadata.
13. After successful publication and verification, run the post-release-finalization agent to consolidate public API files and obsolete compatibility suppressions for human review.
14. Verify links and the externally hosted logo both from the GitHub repository and from NuGet's rendered package README.

Completion criteria:

- A consumer can identify the package's purpose, maturity, runtime requirement, and primary API entry points from NuGet.org.
- All package README links resolve outside the repository checkout.

## 7. Add Automated Package Verification

1. Create a repeatable Release-mode package verification flow.
2. Pack into a clean artifact directory.
3. Inspect the `.nupkg` and `.snupkg` contents and reject:
   - missing expected files;
   - unexpected assemblies;
   - project sources or build artifacts;
   - test assets;
   - lock files;
   - temporary documentation.
4. Validate the NuGet manifest values against the decided package identity.
5. Restore the produced package into an isolated consumer project from a local package source.
6. Compile and run a minimal consumer smoke test against the package rather than a project reference.
7. Verify XML IntelliSense availability and Source Link metadata.
8. Run the existing test suite before accepting the packed artifacts.
9. Verify the published GitHub Package is associated with `FstTesla/MuLang`, remains private, and exposes the expected version.
10. Restore and execute a consumer project against the authenticated published feed, with retry for registry propagation.

Expected main package contents:

- `_rels` and NuGet metadata;
- `MuLang.nuspec`;
- `lib\net10.0\MuLang.dll`;
- `lib\net10.0\MuLang.xml`;
- `README.md`;
- `MuLang.png`;
- `LICENSE`.

Expected symbol package contents:

- NuGet metadata;
- `lib\net10.0\MuLang.pdb`;
- Source Link information required by NuGet tooling.

Completion criteria:

- Package verification is automated and reproducible locally and in CI.
- The isolated consumer uses no repository project reference.
- The published-package consumer uses the registry package rather than the workflow artifact.
- Package association, visibility, and version are verified through the GitHub Packages API.
- Verification fails when package shape, metadata, or basic consumption regresses.

## 8. Add the Public Release Workflow

1. Add a GitHub Actions workflow triggered only by tags matching `v*`.
2. Derive and validate the release version as defined in Section 2.
3. Use the tagged commit as the source checkout; additional Git history is not required for version calculation.
4. Restore in locked mode, build, and test in Release configuration.
5. Pack every packable project into a clean artifact directory using the derived `Version`.
6. Run package verification against the generated artifacts, including metadata, contents, strong-name, XML documentation, Source Link, and an isolated local consumer.
7. Upload `.nupkg` and `.snupkg` as immutable workflow artifacts.
8. Keep package creation and publication in separate jobs so publication consumes the exact artifacts produced and verified by the pack job.
9. Temporarily publish to GitHub Packages at `https://nuget.pkg.github.com/FstTesla/index.json`.
10. Authenticate to GitHub Packages through `actions/setup-dotnet`, using `NUGET_AUTH_TOKEN` populated from the workflow-scoped `GITHUB_TOKEN` and granting the publish job `packages: write`.
11. After publication, verify repository association, private visibility, exact version availability, and an authenticated consumer restore from GitHub Packages.
12. Provide a manual workflow that rebuilds a selected tag and verifies its package against GitHub Packages without publishing a new version.
13. Replace the temporary GitHub Packages publication with NuGet.org trusted publishing when the repository and NuGet.org package ownership are configured.
14. Protect the publishing environment with explicit approval until the release process is established.
15. Prevent duplicate publication from reruns while preserving idempotent build and verification steps.
16. Record the derived version, source tag, commit, and artifact hashes in the workflow summary.
17. Keep branch and pull-request validation separate from publication; those workflows may validate packability but MUST NOT publish or produce official release versions.
18. Fail before package publication when a beta, RC, or stable tag does not contain a non-empty changelog section for the derived version.
19. Allow alpha package publication without a changelog section or GitHub Release.
20. Create the matching GitHub Release after successful beta, RC, or stable package publication using the committed changelog section.

Completion criteria:

- A valid `v` version tag produces verified downloadable artifacts with the version obtained by removing the initial `v`.
- An invalid `v` tag fails without producing package artifacts.
- The publication job uses the exact artifacts emitted by the pack job.
- Temporary publication requires the intended repository, workflow, tag, GitHub Packages namespace, and workflow token permissions.
- Final NuGet.org publication requires the intended repository, workflow, environment, and tag.
- No long-lived NuGet.org API key is stored when trusted publishing replaces the temporary feed.

## 9. Add Continuous Validation

1. Run restore, Debug and Release tests, PublicApiAnalyzers, DocFX, pack, and local package verification for every pull request and push to `main`.
2. Use a temporary prerelease package version that is never published.
3. Upload generated packages only as GitHub Actions artifacts for diagnostic inspection.
4. Do not grant package-write or contents-write permissions to the general validation job.
5. On pushes and same-repository pull requests, resolve the latest published GitHub Package dynamically and run package validation against it.
6. Skip private-baseline validation for pull requests from forks so they never receive package credentials.
7. Support manual validation runs without publishing packages, tags, releases, or documentation.

Completion criteria:

- Every change is built, tested, documented, packed, and locally consumed before merge.
- Public API declaration errors fail validation.
- Trusted changes are compared with the latest published package.
- Fork pull requests run all checks that do not require private package access.
- The workflow contains no package push, release creation, or Pages deployment step.

## 10. Publish `0.1.0-alpha.1`

1. Complete the public API review and baseline.
2. Complete metadata, README, license, symbols, and package verification.
3. Configure the GitHub repository remote and NuGet.org trusted publishing.
4. Confirm that the package ID is available and reserve it through the first publication.
5. Create and push `v0.1.0-alpha.1` from the approved commit.
6. Review the package on NuGet.org:
   - metadata;
   - README rendering;
   - dependencies and target framework;
   - symbols;
   - repository link;
   - license;
   - download and restore.
7. Confirm that subsequent release workflows discover `0.1.0-alpha.1` dynamically as the latest published predecessor.

Completion criteria:

- `MuLang` version `0.1.0-alpha.1` is available and restorable from NuGet.org.
- Published artifacts correspond exactly to the tagged commit.
- Subsequent builds validate API and package compatibility against the release.

## 11. Plan Future Package Boundaries

The initial release remains a single package. Project splitting is not part of package readiness and must not delay the first release.

During the initial prerelease cycle:

1. Track which public APIs are required by ordinary consumers, runtime adapter implementers, and future exporter authors.
2. Identify dependency boundaries among:
   - language model and environment contracts;
   - compiler and binder;
   - portable IR;
   - .NET runtime and exporter.
3. Measure whether consumers would benefit from independently versioned packages rather than merely separate assemblies.
4. Avoid exposing internal implementation types solely to make a future split easier.
5. Prepare a separate design proposal before any split, covering package identities, dependency direction, friend assemblies, public contracts, migration, and compatibility impact.

A split should proceed only when at least one concrete consumer scenario requires independent references, replacement of an implementation component, or independent release cadence.

## Recommended Execution Order

1. Package identity, license, and metadata.
2. Tag-derived workflow versioning.
3. Public API review and XML documentation.
4. API compatibility baseline.
5. Symbols and Source Link.
6. Consumer README and release notes.
7. Automated package verification.
8. GitHub Actions release workflow.
9. Initial NuGet.org publication.
10. Future package-boundary evaluation.

## Remaining Open Points

There are no design decisions blocking implementation.

The following operational value becomes available during execution:

- NuGet.org package ownership and trusted-publishing configuration.
