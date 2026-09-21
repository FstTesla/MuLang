# Solution Split Plan

## Goal

Replace the single production assembly with a clean set of independently testable and packageable projects before implementing additional language features.

The split prioritizes the final architecture over compatibility with the existing alpha packages. All packages share one version, one release tag, and one release cadence.

## Decisions

| Concern | Decision |
|---|---|
| Compatibility | Existing alpha API and assembly compatibility may be broken for a cleaner final design |
| Versioning | Every package uses the same version and release tag |
| Production friend assemblies | Prohibited |
| Test friend assemblies | Permitted only from a production assembly to its corresponding test assembly |
| Test layout | One test project per production project |
| Syntax tree | Internal to the compiler assembly |
| Bound tree | Internal to the compiler assembly |
| IR | Public, immutable, runtime-independent, and supported for alternative exporters |
| .NET exporter package | `MuLang.Exporters.DotNet` |
| Consumer facade | Keep `MuLang` as a thin convenience facade and aggregate package |
| Standard library | Split into runtime-independent declarations and runtime-specific implementations |
| Standard-library placeholders | Create, pack, verify, and publish them from the first split release |

## Target project graph

| Project | Package | Responsibilities | Production dependencies |
|---|---|---|---|
| `MuLang.Core` | `MuLang.Core` | Types, symbols, environment declarations, profiles, diagnostics, source spans, shared runtime-neutral contracts | None |
| `MuLang.IR` | `MuLang.IR` | Public immutable IR model and validation | `MuLang.Core` |
| `MuLang.Compiler` | `MuLang.Compiler` | Lexer, parser, syntax, binding, type checking, flow analysis, lowering, compile-to-IR facade | `MuLang.Core`, `MuLang.IR` |
| `MuLang.Exporters.DotNet` | `MuLang.Exporters.DotNet` | IR exporter, .NET runtime context, provider delegates, adapters, runtime operations | `MuLang.Core`, `MuLang.IR` |
| `MuLang` | `MuLang` | Thin high-level facade composing compiler and .NET exporter | `MuLang.Core`, `MuLang.Compiler`, `MuLang.Exporters.DotNet` |
| `MuLang.StandardLibrary` | `MuLang.StandardLibrary` | Runtime-independent module declarations, stable identifiers, types, constants, and function signatures | `MuLang.Core` |
| `MuLang.StandardLibrary.DotNet` | `MuLang.StandardLibrary.DotNet` | .NET values and function implementations matching the declarative modules | `MuLang.Core`, `MuLang.StandardLibrary`, `MuLang.Exporters.DotNet` |

The dependency graph MUST remain acyclic. In particular:

- `Core` does not reference any higher layer.
- `IR` does not reference the compiler or an exporter.
- `Compiler` does not reference any runtime exporter.
- Exporters do not reference the compiler implementation.
- The standard library does not become a dependency of the compiler or facade.
- Runtime-specific standard-library packages depend on the declarative standard library, never the reverse.

## Public contract strategy

### Core

`MuLang.Core` owns contracts shared by compilers, exporters, hosts, and future standard-library packages:

- language versions and profiles;
- types and type relations;
- environment schemas and provider symbols;
- diagnostics and source spans;
- deterministic fingerprints;
- runtime-neutral error and validation contracts where they are genuinely shared.

Types used only by one higher-level assembly should move to that assembly instead of being promoted merely to cross an assembly boundary.

### IR

`MuLang.IR` is a supported exporter contract.

The public IR surface MUST:

- use immutable objects and read-only collections;
- contain no .NET delegates, expression trees, reflection types, or runtime adapter types;
- encode evaluation order and control flow completely;
- expose enough type and symbol metadata for a mechanical exporter;
- retain environment, profile, compilation-mode, and source-span metadata;
- provide public validation for programs received by third-party exporters.

The compiler must be able to construct IR without production `InternalsVisibleTo`. Therefore IR constructors or public factories must form a complete supported construction API. Mutable builders remain internal to `MuLang.Compiler`.

### Compiler

`MuLang.Compiler` exposes a runtime-independent compile-to-IR facade and result type.

The facade MUST:

- accept source text, environment schema, profile, compilation mode, and expected result type;
- return diagnostics and an optional validated `IrProgram`;
- perform lexing, parsing, binding, flow analysis, and lowering;
- never reference the .NET exporter or return a runtime-specific delegate.

Syntax nodes, bound nodes, flow state, parser, binder, and lowerer remain internal. They may be exposed only to `MuLang.Compiler.Tests`.

### .NET exporter

`MuLang.Exporters.DotNet` exposes:

- the public exporter entry point from validated `IrProgram` to a .NET delegate;
- `DotNetRuntimeContext`;
- provider-function delegate contracts;
- object and array adapter interfaces;
- runtime-specific export results and exceptions.

The exporter validates IR and compilation identity but does not repeat name resolution, type inference, or high-level lowering.

### Facade

The `MuLang` package remains the simple entry point for consumers targeting .NET.

Its assembly should contain only:

- a high-level compiler facade;
- a .NET-oriented compilation result;
- minimal orchestration between `MuLang.Compiler` and `MuLang.Exporters.DotNet`.

It must not duplicate core, compiler, IR, or exporter implementation. Consumers creating alternative exporters can reference `MuLang.Compiler`, `MuLang.IR`, and `MuLang.Core` without referencing `MuLang` or the .NET exporter.

## Current source mapping

| Current location | Target project |
|---|---|
| `src\MuLang\Core\**` | `src\MuLang.Core` |
| `src\MuLang\IR\**` | `src\MuLang.IR` |
| `src\MuLang\Compiler\**` | `src\MuLang.Compiler` |
| `src\MuLang\Exporters\DotNet\**` | `src\MuLang.Exporters.DotNet` |
| `src\MuLang\MuLangCompiler.cs` | redesign and move to `src\MuLang` facade |
| `src\MuLang\CompilationResult.cs` | redesign and keep in `src\MuLang` facade |

The move should preserve namespaces that still describe the final responsibility. Namespace changes are permitted when they improve the final contract and must be handled as intentional alpha breaking changes.

## Visibility rules

For every type referenced across production projects, choose exactly one:

1. It is a supported cross-assembly contract and becomes public.
2. It belongs in the consuming lower-level project and is relocated.
3. The dependency is redesigned so the type does not cross the boundary.

Do not use production `InternalsVisibleTo` as a fourth option.

Each production assembly may expose its internals only to its matching test assembly:

- `MuLang.Core` → `MuLang.Core.Tests`;
- `MuLang.IR` → `MuLang.IR.Tests`;
- `MuLang.Compiler` → `MuLang.Compiler.Tests`;
- `MuLang.Exporters.DotNet` → `MuLang.Exporters.DotNet.Tests`;
- `MuLang` → `MuLang.Tests`;
- future standard-library assemblies → their matching test assemblies.

## Test project partition

| Test project | Initial test ownership |
|---|---|
| `MuLang.Core.Tests` | Text, types, environment, profiles, diagnostics, fingerprints, symbols |
| `MuLang.IR.Tests` | IR model, validator, metadata, malformed-IR robustness |
| `MuLang.Compiler.Tests` | Lexer, parser, binder, type checking, flow, lowering, recursion analysis, feature restrictions |
| `MuLang.Exporters.DotNet.Tests` | Exporting, runtime operations, adapters, execution budget, cancellation, runtime call depth |
| `MuLang.Tests` | End-to-end facade compilation and execution |
| `MuLang.StandardLibrary.Tests` | Declarative module composition and collision behavior when implemented |
| `MuLang.StandardLibrary.DotNet.Tests` | Runtime implementations and declaration/implementation parity when implemented |

Tests that currently mix compiler and runtime assertions must be split by responsibility rather than assigned wholesale to one project.

Shared test utilities should be minimized. If needed, create a non-packable `MuLang.Testing` project containing only public test helpers; do not introduce friend access from multiple production assemblies to one general test assembly.

## Standard-library placeholders

Create these projects during the solution split:

- `src\MuLang.StandardLibrary\MuLang.StandardLibrary.csproj`;
- `src\MuLang.StandardLibrary.DotNet\MuLang.StandardLibrary.DotNet.csproj`;
- matching test projects.

Until real modules are designed:

- keep both production projects packable and include them in every release;
- assign final package identities, descriptions, metadata, README content, symbol packages, and Source Link from their first version;
- do not add placeholder public APIs merely to produce a non-empty assembly;
- allow their initial public API baselines to contain no API entries beyond `#nullable enable`;
- include them in package verification, publication, post-publish verification, documentation, and dependency validation;
- document the intended dependency direction.

The future declarative package will describe modules independently from runtime implementations. Import remains explicit and selective; no standard-library symbol is added implicitly by the compiler or facade.

Publishing the placeholders establishes package ownership and dependency direction without committing premature module APIs.

## API compatibility reset

The split is an intentional alpha-level package and assembly redesign.

For the first release containing the split:

1. Document the package graph and breaking changes in `CHANGELOG.detailed.md`.
2. Disable the previous-package baseline explicitly for that release.
3. Generate a fresh `PublicAPI.Shipped.txt` for every distributed project from its intended final public surface.
4. Reset every `PublicAPI.Unshipped.txt` to `#nullable enable`.
5. Start with empty compatibility-suppression files unless a package has a meaningful same-identity baseline.
6. Treat each new package as having no previous package baseline.
7. Resume dynamic package validation independently for each package after the split release is published.

Do not carry obsolete single-assembly suppression entries into the new projects.

## Packaging and release changes

All distributed packages share the same version and are released from the same `v<version>` tag.

Required changes:

- replace the globally fixed `PackageId` with per-project package identity;
- retain shared author, license, repository, icon, signing, Source Link, and symbol settings centrally;
- provide package-specific titles, descriptions, tags, and README strategy;
- produce package references matching the production dependency graph;
- generalize baseline discovery to resolve a predecessor per package ID;
- generalize package verification to validate every expected package and dependency;
- publish all package artifacts before post-publish verification;
- verify every published package is associated with the repository and has the expected visibility;
- keep aggregate-package consumer verification and add direct-package consumer tests.

The release workflow must fail if any expected package or symbol package is missing.

## Public API governance

Each distributed project owns:

- `PublicAPI.Shipped.txt`;
- `PublicAPI.Unshipped.txt`;
- `CompatibilitySuppressions.xml` when needed;
- a private `Microsoft.CodeAnalysis.PublicApiAnalyzers` reference.

CI validates every project. Release preflight and post-release finalization agents must operate on the complete package set rather than assuming one `MuLang` assembly.

## Documentation changes

DocFX must consume all distributed assemblies and generate one API section grouped by package or assembly.

Conceptual documentation must include:

- package selection guidance;
- dependency graph;
- high-level facade usage;
- compile-to-IR usage for exporter authors;
- .NET exporter usage;
- future standard-library import model.

The language specification remains independent of the assembly split.

## Implementation phases

### Phase 1: Freeze and inventory

1. Record the current package contents, public API files, project references, and test ownership.
2. Produce a type-level map of every internal symbol referenced across planned boundaries.
3. Classify each crossing as public contract, relocation, or redesign.
4. Finalize package descriptions and dependency graph.

Exit criteria:

- every production file and test file has one target project;
- every cross-project type has a visibility decision;
- no dependency cycle exists in the proposed graph.

### Phase 2: Scaffold projects

1. Create all production and test projects.
2. Add project references according to the target graph.
3. Add test-only `InternalsVisibleTo` declarations.
4. Add packable standard-library placeholders with final package identities and metadata.
5. Update the solution.

Exit criteria:

- the empty graph restores and builds;
- reference direction is enforceable before source moves begin.

### Phase 3: Extract Core

1. Move runtime-independent core types.
2. Move corresponding tests.
3. Resolve accidental dependencies from Core to higher layers.
4. Establish the new public API baseline for Core.

Exit criteria:

- `MuLang.Core` and `MuLang.Core.Tests` build and pass independently;
- Core references no other production project.

### Phase 4: Extract and publish IR contracts

1. Move IR nodes and validator.
2. Make the immutable exporter-facing model public.
3. Keep mutable construction helpers out of the IR assembly.
4. Move and expand IR tests.
5. Establish the IR public API baseline.

Exit criteria:

- a separate assembly can inspect and validate IR using only public APIs;
- IR has no compiler or runtime-specific dependency.

### Phase 5: Extract Compiler

1. Move lexer, parser, binder, flow analysis, and lowering.
2. Introduce the public compile-to-IR facade and result.
3. Move compiler tests and split mixed runtime tests.
4. Remove any compiler dependency on the .NET exporter.
5. Establish the compiler public API baseline.

Exit criteria:

- source can be compiled into validated IR without referencing a runtime exporter;
- syntax and bound models remain internal.

### Phase 6: Extract .NET exporter

1. Move exporter and runtime-specific types.
2. Introduce the public exporter entry point.
3. Move runtime/exporter tests.
4. Ensure all IR consumption uses only the public IR contract.
5. Establish the exporter public API baseline.

Exit criteria:

- the exporter can be replaced by another assembly without compiler changes;
- runtime adapters remain .NET-specific.

### Phase 7: Rebuild the facade

1. Redesign the `MuLang` facade around compiler and exporter public APIs.
2. Keep orchestration thin.
3. Move only end-to-end tests into `MuLang.Tests`.
4. Remove duplicated implementation from the facade assembly.

Exit criteria:

- ordinary .NET consumers retain one simple package and entry point;
- exporter authors do not depend on the facade.

### Phase 8: Add standard-library placeholders

1. Add declarative and .NET implementation projects.
2. Add their matching test projects.
3. Configure both production projects as packable with final package IDs and shared release versioning.
4. Add package-specific descriptions and README guidance stating that no modules are available yet.
5. Add empty shipped/unshipped API baselines without inventing placeholder APIs.
6. Include both packages and symbol packages in verification and publication.
7. Verify dependency direction and absence of implicit environment changes.

Exit criteria:

- both standard-library packages are published and verifiable without committing premature module APIs;
- their package dependencies already enforce the intended declarative/runtime direction.

### Phase 9: Generalize packaging and automation

1. Update common MSBuild metadata and per-package metadata.
2. Generalize package verification and baseline resolution.
3. Update CI, publication, manual verification, release preflight, changelog, and post-release agents.
4. Update DocFX assembly inputs and API navigation.
5. Reset API baselines for the split release.

Exit criteria:

- CI builds, tests, packs, and verifies every package;
- publication handles the entire package set atomically;
- documentation covers every public assembly.

### Phase 10: Remove the monolith

1. Delete the old combined project and obsolete files.
2. Remove transitional references and temporary compatibility shims.
3. Verify namespace ownership and package contents.
4. Run full Debug and Release validation.

Exit criteria:

- no production source remains in the obsolete monolithic project;
- no production friend assembly exists;
- every test is owned by the corresponding project;
- all packages and symbol packages pass verification.

## Final acceptance criteria

- The solution has the target acyclic project graph.
- Every production assembly has one clear responsibility.
- No production assembly uses `InternalsVisibleTo` for another production assembly.
- Every production project has a corresponding test project.
- Compiler usage does not require a runtime exporter.
- Alternative exporters can consume and validate public IR.
- The `MuLang` facade remains thin and convenient for .NET consumers.
- Packable standard-library placeholders preserve and publish the future declarative/runtime split.
- All distributed packages share one version and tag.
- CI, release, verification, API compatibility, and DocFX operate on the complete package set.
- The split release starts clean per-package API baselines.
- Existing language semantics and tests remain unchanged except for intentional API/package redesign.

## Open points

There are no blocking architecture questions for planning.

Exact public type names for the compile-to-IR facade, IR construction API, exporter result, and facade result should be finalized during Phase 1 after the cross-boundary type inventory.
