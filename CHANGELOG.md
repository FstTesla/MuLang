# Changelog

This file records consolidated consumer-visible changes for stable MuLang versions.

Changes are classified as:

- breaking changes;
- new features;
- fixes.

Each release section describes the final stable outcome since the preceding stable version. Intermediate prerelease changes that were superseded before the stable release are omitted.

Each release heading identifies the stable version range covered by the section, from the preceding stable version to the released version.

## `0.1.0` → `0.2.0` - 2026-10-02

### Breaking changes

- Changed the default language version from version 1 to version 1.1 for compiler, lexer, parser, profile-builder, and environment-builder APIs. Hosts that require version 1 behavior must now select it explicitly.
- Removed the public `LanguageProfile` constructor in favor of `LanguageProfileBuilder`. Standard profiles now enable constant folding, and profile and environment fingerprints also include constant-folding and array-mutability semantics; persisted IR produced by version 0.1 must be recompiled.
- Changed [`is` and `as` checked-cast semantics](https://fsttesla.github.io/MuLang/language/05-assignability-and-conversions.html#58-explicit-checked-casts) to use identity-preserving runtime conformance. Checked casts no longer perform primitive-to-string, integer-to-float, or concrete numeric-kind conversions.
- Reserved `infty` and `nan` in language version 1.1 and changed culture-independent formatting of IEEE 754 infinity and NaN to their MuLang source spellings.
- Replaced `IrInstruction.Convert.IsChecked` with `IrInstruction.Convert.Kind` and `IrConversionKind`. Parameter slots are now explicitly read-only, and custom IR producers must use mutable locals for reassigned values.
- Changed `DotNetExportResult` construction and deconstruction to use its result-returning `ExecutionDelegate`; the exception-based `Delegate` remains only as a derived compatibility view. The legacy `DotNetFunction`, matching runtime-context constructor, and string-based `MuLangRuntimeException` constructor are obsolete in favor of the context-aware provider and structured-error contracts.

### New features

- Added language version 1.1 with [read-only array types](https://fsttesla.github.io/MuLang/language/04-types.html#46-array-types), read-only array literals, read-only local variables, binary/octal/hexadecimal integer literals, and `infty` and `nan` float literals.
- Added compile-time constant folding and semantic warnings for statically redundant or deterministic conditions, null operations, type tests, checked casts, and empty statements.
- Added the versioned canonical [MuIR serialization format](https://fsttesla.github.io/MuLang/ir/muir-format.html), public reader and writer APIs, configurable resource limits, mutable/read-only IR slots, and atomic construction of recursive structured-object type graphs.
- Added a Visual Studio extension for `.mu` and `.mulang` files with lexical and semantic highlighting, live diagnostics, and an embedded language server, together with compiler APIs for analysis and source classification.
- Added structured runtime errors and result-returning execution, context-aware provider functions with metered value inspection and cancellation, explicit expected provider failures, and preserved underlying host failures for diagnostics.
- Added an explicitly opt-in [standard library](https://fsttesla.github.io/MuLang/standard-library.html) with versioned symbol selection and .NET implementations for math, arrays, objects, strings, parsing, Base64, randomness, clocks, and GUIDs.

### Fixes

- Language version 1 now reports that `$` read-only syntax requires version 1.1 instead of treating it as an invalid source character.
- Missing-return diagnostics now point to the end of a top-level program or to a function's closing brace instead of highlighting the complete body.
- Statically guaranteed checked casts no longer consume runtime execution budget or fail because of cancellation or traversal limits.
