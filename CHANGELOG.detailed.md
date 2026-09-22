# Detailed changelog

This file records consumer-visible changes for stable, release-candidate, beta, and alpha versions, including releases with no consumer-visible changes.

Changes are classified as:

- breaking changes;
- new features;
- fixes.

Stable entries describe the incremental change since the preceding prerelease. The main `CHANGELOG.md` provides the consolidated history between stable versions.

Each release heading identifies the incremental version range covered by the section, from the comparison version to the released version.

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
