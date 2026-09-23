# Detailed changelog

This file records consumer-visible changes for stable, release-candidate, beta, and alpha versions, including releases with no consumer-visible changes.

Changes are classified as:

- breaking changes;
- new features;
- fixes.

Stable entries describe the incremental change since the preceding prerelease. The main `CHANGELOG.md` provides the consolidated history between stable versions.

Each release heading identifies the incremental version range covered by the section, from the comparison version to the released version.

## `0.1.0` → `0.2.0-alpha.1` - 2026-09-23

### Breaking changes

- Changed the default language version from version 1 to version 2 for compiler, lexer, parser, profile-builder, and environment-builder APIs. Hosts that require version 1 behavior must now select it explicitly.
- Changed environment fingerprints for schemas containing array signatures because array mutability capability now contributes to type canonicalization. Previously persisted IR using those fingerprints must be recompiled.

### New features

- Added language version 2 with covariant read-only array views (`T[]$`) and read-only array literals (`$[...]`).
- Added read-only array common-type inference for conditional expressions, null coalescing, and nested array literals. Compatible array operands may now produce a read-only common type where compilation previously failed.
- Added shape-based array `is` and `as` validation, identity-preserving mutable-to-read-only views, and checked acquisition of mutable capability. `$[...]` values expose only read capability, while readonly views originating from mutable arrays can recover write capability through a successful checked cast.
- Array elements are revalidated against their static element type on every read, and provider arguments are recursively validated before invocation. Shape changes introduced through another mutable alias therefore produce a runtime type error before an invalid value is observed by MuLang code or a provider.
- Read-only provider parameters are source-level contracts. Trusted .NET provider implementations receive the original validated adapter and may observe additional host interfaces implemented by that value.
- Added the independent `IDotNetReadOnlyArrayValue` adapter contract while preserving the existing `IDotNetArrayValue` API for mutable provider arrays.

### Fixes

- Changed language version 1 handling of `$` and `$[` to report that the syntax requires language version 2 instead of reporting invalid source characters.

## `0.1.0-rc.1` → `0.1.0` - 2026-09-23

No consumer-visible changes.

## `0.1.0-alpha.5` → `0.1.0-rc.1` - 2026-09-22

No consumer-visible changes.

## `0.1.0-alpha.4` → `0.1.0-alpha.5` - 2026-09-22

No consumer-visible changes.

## `0.1.0-alpha.3` → `0.1.0-alpha.4` - 2026-09-22

No consumer-visible changes.

## `0.1.0-alpha.2` → `0.1.0-alpha.3` - 2026-09-22

### Breaking changes

- Replaced the `MuLang` package with the separately referenceable `MuLang.Core`, `MuLang.IR`, `MuLang.Compiler`, `MuLang.Exporters.DotNet`, `MuLang.StandardLibrary`, and `MuLang.StandardLibrary.DotNet` packages. Existing .NET hosts must replace their `MuLang` reference with at least `MuLang.Compiler` and `MuLang.Exporters.DotNet`. See the [package architecture](https://fsttesla.github.io/MuLang/packages.html) for the complete dependency graph.
- Replaced `MuLang.MuLangCompiler.CompileExpression` and `CompileProgram` with [`MuLang.Compiler.MuLangCompiler.Compile`](https://fsttesla.github.io/MuLang/api/MuLang.Compiler.MuLangCompiler.html). Compilation now produces portable IR through `CompilationResult.Program`; .NET hosts must explicitly call [`DotNetExporter.Export`](https://fsttesla.github.io/MuLang/api/MuLang.Exporters.DotNet.DotNetExporter.html) instead of reading `CompilationResult.Delegate`.

### New features

- Added the public [`MuLang.IR`](https://fsttesla.github.io/MuLang/api/MuLang.IR.IrProgram.html) model and validation APIs, enabling runtime-independent compilation and custom exporters.
- Exposed compiler, IR, and .NET runtime diagnostic codes as public constants.
- Exposed `ObjectTypeSymbol.CreateAnonymous`, `TypeSymbols.Null`, and `TypeSymbols.Error` for compiler and IR integrations.

## `0.1.0-alpha.1` → `0.1.0-alpha.2` - 2026-09-18

### Breaking changes

- Changed the numeric value of [`OpenObjectsFeature.Enabled`](https://fsttesla.github.io/MuLang/api/MuLang.Core.OpenObjectsFeature.html) from `1` to `2`.

### New features

- Added [`OpenObjectsFeature.PropertyExistenceOnly`](https://fsttesla.github.io/MuLang/api/MuLang.Core.OpenObjectsFeature.html#MuLang_Core_OpenObjectsFeature_PropertyExistenceOnly), which enables dynamic `has` tests without enabling open-object creation, access, mutation, or provider-declared open types. See the [open-object profile semantics](https://fsttesla.github.io/MuLang/language/18-language-profiles.html#185-open-objects) for details.
