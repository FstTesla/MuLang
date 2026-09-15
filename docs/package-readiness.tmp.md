# Package Readiness Plan

## Goal

Prepare `MuLang` for an initial public release to NuGet.org as a single package, with complete public API documentation, deterministic Git-derived versioning, package metadata, source debugging support, automated API compatibility checks, and repeatable package validation.

The initial release target is `0.1.0-alpha.1`. Release versions are derived from Git tags using the format `v<major>.<minor>.<patch>` with an optional SemVer prerelease suffix.

## Decisions

| Concern | Decision |
|---|---|
| Distribution | Public package on NuGet.org |
| Package ID | `MuLang` |
| Authors | `FstTesla` |
| License | Apache-2.0 |
| Repository and project URL | `https://github.com/FstTesla/MuLang` |
| Initial version | `0.1.0-alpha.1` |
| Version source | Git tags |
| Tag format | `v<major>.<minor>.<patch>[-<prerelease>]` |
| Package structure | One package for the initial releases |
| API compatibility | Automatically enforced |
| Symbols and sources | Publish a symbol package with Source Link |
| Package icon | Deferred |

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
   - README;
   - copyright;
   - package release notes source.
4. Set `PackageRequireLicenseAcceptance` to `false`, consistently with common Apache-2.0 package distribution.
5. Keep the package icon unset until an approved asset exists rather than publishing a placeholder.
6. Ensure the README and license are included at the package root.

Completion criteria:

- `dotnet pack` emits no package metadata warnings.
- The generated manifest contains all decided metadata.
- NuGet clients display the README and license correctly.

## 2. Introduce Git-Derived Versioning

1. Adopt MinVer as a private build dependency because the required versioning model is tag-based and does not require generated version files.
2. Use a floating major package version, consistently with the repository dependency policy, and commit the resulting lock-file update.
3. Configure `v` as the tag prefix.
4. Define the prerelease identifier used by ordinary untagged development builds.
5. Treat a SemVer tag on the commit being built as the source of truth for an official package version.
6. Verify the following cases:
   - repository state before the first release tag;
   - exact prerelease tag such as `v0.1.0-alpha.1`;
   - commits after a release tag;
   - stable tag such as `v1.0.0`;
   - shallow CI checkout with sufficient Git history and tags.
7. Document the release-tag convention and prohibit manually overriding `PackageVersion` in normal release builds.

Completion criteria:

- Local and CI builds calculate the same version for the same commit.
- A build of `v0.1.0-alpha.1` produces `MuLang.0.1.0-alpha.1.nupkg`.
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

1. Add `Microsoft.CodeAnalysis.PublicApiAnalyzers` as a private development dependency using a floating major version.
2. Generate and review the initial shipped public API baseline before the first package release.
3. Require intentional API additions and removals to update the baseline explicitly.
4. Enable .NET SDK package validation for release package builds.
5. Publish `0.1.0-alpha.1` without a previous-package comparison because no baseline package exists yet.
6. After the first release, configure package validation against the selected previous release.
7. Define the prerelease compatibility policy:
   - compatibility breaks are permitted before `1.0.0`;
   - they must still be explicit in the API baseline and release notes;
   - accidental breaks must fail validation;
   - stable releases follow SemVer compatibility requirements.
8. Keep suppressions local, documented, and limited to intentional compatibility changes.

Completion criteria:

- Unreviewed public API changes fail the build.
- Package validation detects binary or source compatibility regressions against the configured baseline.
- Intentional prerelease breaks remain visible in version control and release notes.

## 5. Configure Symbols, Determinism, and Source Link

1. Preserve deterministic and continuous-integration build settings already defined in `Directory.Build.props`.
2. Configure repository metadata required by Source Link.
3. Confirm whether the .NET 10 SDK provides the required GitHub Source Link integration without an additional package; add a private Source Link dependency only if validation proves it necessary.
4. Produce portable PDB files.
5. Generate a `.snupkg` symbol package.
6. Include source revision information in assembly informational metadata.
7. Verify that published PDB documents resolve to immutable commit-specific GitHub URLs.
8. Verify source debugging from a clean consumer project using only the `.nupkg` and `.snupkg`.

Completion criteria:

- Packing produces both `.nupkg` and `.snupkg`.
- The symbol package is accepted by NuGet.org validation.
- A debugger can retrieve the matching source for a packaged assembly.

## 6. Improve Consumer Documentation

1. Expand the root README from a repository overview into the package landing page.
2. Document:
   - package purpose and current prerelease status;
   - supported target framework;
   - installation;
   - the compiler/environment/runtime workflow;
   - links to the normative language specification;
   - compatibility and versioning expectations;
   - license and repository links.
3. Keep detailed language semantics in `docs\language-specification.md` and avoid duplicating normative content in the README.
4. Add release notes for `0.1.0-alpha.1`, focused on supported capabilities and known prerelease limitations.
5. Verify links both from the GitHub repository and from NuGet's rendered package README.

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

Expected main package contents:

- `_rels` and NuGet metadata;
- `MuLang.nuspec`;
- `lib\net10.0\MuLang.dll`;
- `lib\net10.0\MuLang.xml`;
- `README.md`;
- `LICENSE`.

Expected symbol package contents:

- NuGet metadata;
- `lib\net10.0\MuLang.pdb`;
- Source Link information required by NuGet tooling.

Completion criteria:

- Package verification is automated and reproducible locally and in CI.
- The isolated consumer uses no repository project reference.
- Verification fails when package shape, metadata, or basic consumption regresses.

## 8. Add the Public Release Workflow

1. Add a GitHub Actions workflow triggered by matching version tags.
2. Use a full checkout with tags available for version calculation.
3. Restore in locked mode, build, test, pack, and run package verification.
4. Upload `.nupkg` and `.snupkg` as immutable workflow artifacts.
5. Publish through NuGet.org trusted publishing when the repository and NuGet.org package ownership are configured.
6. Protect the publishing environment with explicit approval until the release process is established.
7. Prevent duplicate publication from reruns while preserving idempotent build and verification steps.
8. Record the package version, commit, and artifact hashes in the workflow summary.
9. Keep pull-request validation separate from publication; pull requests must build and validate packaging but never push packages.

Completion criteria:

- A version tag produces verified downloadable artifacts.
- Publication requires the intended repository, workflow, environment, and tag.
- No long-lived NuGet API key is stored if trusted publishing is available.

## 9. Publish `0.1.0-alpha.1`

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
7. Configure `0.1.0-alpha.1` as the first package-validation baseline for subsequent releases.

Completion criteria:

- `MuLang` version `0.1.0-alpha.1` is available and restorable from NuGet.org.
- Published artifacts correspond exactly to the tagged commit.
- Subsequent builds validate API and package compatibility against the release.

## 10. Plan Future Package Boundaries

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
2. Git-derived versioning.
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

The following operational values become available during execution:

- approved copyright text and year;
- package description and tags after editorial review;
- development-build prerelease identifier;
- exact floating major versions for MinVer and PublicApiAnalyzers;
- NuGet.org package ownership and trusted-publishing configuration;
- package icon for a later release;
- previous-package baseline selection after `0.1.0-alpha.1` is published.
