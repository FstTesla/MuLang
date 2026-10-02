# Detailed changelog

This file records consumer-visible changes for stable, release-candidate, beta, and alpha versions, including releases with no consumer-visible changes.

Changes are classified as:

- breaking changes;
- new features;
- fixes.

Stable entries describe the incremental change since the preceding prerelease. The main `CHANGELOG.md` provides the consolidated history between stable versions.

Each release heading identifies the incremental version range covered by the section, from the comparison version to the released version.

## `0.2.0-alpha.7` → `0.2.0-rc.1` - 2026-10-02

No consumer-visible changes.

## `0.2.0-alpha.6` → `0.2.0-alpha.7` - 2026-10-01

### Breaking changes

- Marked `MuLangRuntimeException(string, string, TextSpan, Exception?)` obsolete. Custom runtimes and exporters should construct a structured `RuntimeError` and use `MuLangRuntimeException(RuntimeError, Exception?)`; MuLang no longer uses the compatibility overload internally.
- Marked the legacy `DotNetFunction` delegate and matching `DotNetRuntimeContext` constructor obsolete. Providers should implement `DotNetProviderFunction` and register functions through `DotNetRuntimeContext.Create`.
- Changed `DotNetExportResult` to store and deconstruct its result-returning `ExecutionDelegate` instead of its exception-based `Delegate`. The exporter now generates the result-returning surface directly; `Delegate` is a derived compatibility view and is no longer init-settable.

### New features

- Added structured runtime errors with stable categories, explicit catchability, source spans, MuLang stack frames, public causes, and application payloads while preserving the existing `MuLangRuntimeException.Code` and `Span` API.
- Added `ExecutionResult` and `DotNetExportResult.ExecutionDelegate` as the canonical result-returning execution surface, including preserved underlying host failures for diagnostics. The existing exception-based delegate is now derived from it as a compatibility view.
- Added the context-aware `DotNetProviderFunction` and `DotNetProviderInvocationContext` contracts for cancellation, metered read-only array inspection, controlled lazy object-property enumeration and reads, MuLang structural equality, and application-error reporting.
- Added `MuLangProviderException` as the explicit contract for expected provider application failures. Expected failures become catchable `Application` errors; unexpected provider exceptions become uncatchable `Provider` errors.
- Turned the previously placeholder `MuLang.StandardLibrary` and `MuLang.StandardLibrary.DotNet` packages into a functional, explicitly opt-in standard library. The compiler, exporter, and facade packages do not import it automatically.
- Added a versioned symbol catalog for math, arrays, objects, strings, parsing, Base64 text encoding, randomness, clocks, and GUIDs. Types, globals, functions, and proposed modules expose canonical static properties, read-only collections, and ordinal lookup by provider ID or language/display name.
- Added normalized symbol selections with dependency expansion, deduplication, per-symbol language-version and capability metadata, and modules whose aggregate requirements are derived from their members. Hosts can select individual symbols, complete modules, or both.
- Added runtime-independent environment composition and matching .NET binding from the same selection, including composition with host declarations. `DotNetStandardLibrary` captures configurable services and produces immutable runtime globals and provider functions only for selected symbols.
- Added .NET implementations with Unicode-scalar string indexing, ordinal and culture-independent text behavior, MuLang structural equality for array membership, sorted object projections, read-only collection results, nullable parse and decoding failures, and application errors for invalid ranges, overflow, and impossible conversions.
- Added configurable random sources and time providers for the selected Random, Clock, and GUID functions, with thread-safe random and system-clock defaults.
- Added declaration collision checks and catalog-wide .NET declaration-to-implementation parity validation, including duplicate identifiers and names, host conflicts, unsupported or noncanonical selections, and incompatible runtime globals.

## `0.2.0-alpha.5` → `0.2.0-alpha.6` - 2026-09-30

### New features

- Added language version 1.1 [read-only local variables](https://fsttesla.github.io/MuLang/language/06-variables-and-scope.html#61-local-variables), denoted by `$` after the declaration name. They support declaration-time or delayed initialization, mutually exclusive assignment sites, definite-assignment analysis, portable IR validation, and semantic highlighting.
- Added semantic warnings for redundant empty statements, statically constant Boolean and truthiness conditions, deterministic null coalescing and null comparisons, redundant or always-null optional access, always-true type tests, and checked casts to equivalent types. Warning analysis is independent of the constant-folding profile setting; constant-evaluation failures use `MUL3031` as a warning when folding is disabled and retain the existing error when folding is enabled.

## `0.2.0-alpha.4` → `0.2.0-alpha.5` - 2026-09-29

### New features

- Added a Visual Studio 2022 and 2026 VSIX for `.mu` and `.mulang` files with TextMate lexical highlighting, compiler-bound semantic highlighting, live diagnostics, and an embedded out-of-process .NET 10 language server.
- Added tagged VSIX artifacts with release-derived numeric extension versions. Beta, release-candidate, and stable GitHub Releases include the VSIX as a downloadable asset.
- Added editor-oriented compiler APIs for analysis without IR generation, lexical classification, and binding-based semantic classification. `LanguageProfiles.Latest` selects the latest standard profile, and `SourceText.GetUtf16Offset` maps MuLang scalar offsets to UTF-16 offsets for editor protocols.
- Added language version 1.1 binary (`0b`), octal (`0o`), and hexadecimal (`0x`) integer literals with case-insensitive prefixes, signed 64-bit boundary handling, version-1 diagnostics, specification coverage, and Visual Studio lexical highlighting.

### Fixes

- Changed missing-return diagnostic `MUL4004` to identify the end of a top-level program or the closing brace of a function instead of highlighting the complete program or function body.

## `0.2.0-alpha.3` → `0.2.0-alpha.4` - 2026-09-28

### Breaking changes

- Reserved `infty` and `nan` as keywords in language version 1.1. Environment schemas targeting version 1.1 can no longer expose provider types, globals, functions, or parameters with those language names; version 1 continues to treat them as identifiers.
- Changed the culture-independent string conversion of positive infinity, negative infinity, and NaN from `Infinity`, `-Infinity`, and `NaN` to the source-compatible spellings `infty`, `-infty`, and `nan`.
- Changed the [portable IR slot contract](https://fsttesla.github.io/MuLang/ir/portable-intermediate-representation.html) so parameter slots are explicitly read-only. The existing `IrSlot` constructor now gives parameter slots read-only capability, and `IrValidator` rejects instructions that define them. Custom IR producers must use a mutable local for values that require reassignment.

### New features

- Added the language version 1.1 `infty` and `nan` float literals for IEEE 754 positive infinity and NaN. The parser accepts separate leading signs, including `-infty`, `+nan`, and `-nan`, while finite decimal literals that overflow remain compile-time errors.
- Added lexical warning `MUL1005` for every use in an earlier language version of an identifier spelling that becomes reserved in a later supported version. Version 1 code may still use `infty` and `nan`, but now receives migration warnings because version 1.1 reserves them.
- Added the versioned, canonical, textual [MuIR (`.muir`) format](https://fsttesla.github.io/MuLang/ir/muir-format.html) for serializing and deserializing portable `IrProgram` graphs. `MuLang.IR` now provides UTF-8 and text APIs, explicit polymorphic wire tokens, source-span preservation, strict parsing diagnostics, configurable resource limits, and byte-stable round trips.
- Added atomic construction of immutable self-recursive and mutually recursive structured-object type graphs for provider environments and portable IR. Type equivalence and MuIR canonicalization now terminate coinductively, normalize object properties ordinally, merge bisimilar wire graphs, and support forward type references. Lowering projects provider object types to structural IR-only graphs, and MuIR omits provider IDs, type names, and named-versus-anonymous origin.
- Added mutable/read-only capability to IR slots while preserving existing construction. Local and temporary slots default to mutable, MuIR persists the capability, and `IrValidator` requires a read-only local to have exactly one syntactic definition site.

## `0.2.0-alpha.2` → `0.2.0-alpha.3` - 2026-09-24

### Breaking changes

- Renamed language version 2 to language version 1.1 without changing its semantics or numeric value. `LanguageVersion.Version2` and `LanguageProfiles.Version2` are now `LanguageVersion.Version1_1` and `LanguageProfiles.Version1_1`.
- Removed the public `LanguageProfile` constructor. Hosts must now create or customize profiles through [`LanguageProfileBuilder`](https://fsttesla.github.io/MuLang/api/MuLang.Core.LanguageProfileBuilder.html).
- Enabled [compile-time constant folding](https://fsttesla.github.io/MuLang/language/08-expressions.html#811-compile-time-constant-evaluation) in both standard language profiles. Integer overflow, integer division or remainder by zero, invalid shift counts, and failed checked casts in required constant expressions now produce compilation error `MUL3031` and no executable artifact instead of failing at runtime. Hosts that require the previous behavior must disable constant folding in their profile.
- Changed every standard language-profile fingerprint by adding constant folding to the profile identity. Cached or persisted IR compiled with earlier fingerprints must be recompiled.

### New features

- Added primitive constant folding before lowering for intrinsic operators, value conversions, checked casts, type tests, truthiness, null coalescing, conditional expressions, and conditional Boolean operators while preserving evaluation order and short-circuit behavior.
- Added the [`ConstantFoldingFeature`](https://fsttesla.github.io/MuLang/language/14-language-profiles.html#149-compile-time-constant-evaluation) profile option, the `LanguageProfile.ConstantFolding` property, and `LanguageProfileBuilder.WithConstantFolding`. Disabling the feature preserves runtime evaluation and runtime failures for constant expressions.

## `0.2.0-alpha.1` → `0.2.0-alpha.2` - 2026-09-24

### Breaking changes

- Changed [`is` and `as` checked-cast semantics](https://fsttesla.github.io/MuLang/language/05-assignability-and-conversions.html#58-explicit-checked-casts) to use the same runtime-conformance relation. A statically permitted `value as T` now succeeds exactly when `value is T` is `true` for the same unchanged value, and a successful cast preserves the runtime representation and identity. Consequently, `as` no longer converts primitive values or `null` to `string`, promotes `int` to `float`, or converts between the concrete runtime kinds represented by `number`. Use string concatenation for primitive formatting, `integer + 0.0` to promote a statically typed `int`, and standard-library numeric functions for intentional numeric transformations.
- Replaced `IrInstruction.Convert.IsChecked` with the `IrInstruction.Convert.Kind` property and `IrConversionKind` enum. Custom IR producers and exporters must distinguish value conversions from checked casts through the new constructor parameter.
- Restricted `TypeRelations.ClassifyConversion` and `IrConversionKind.ValueConversion` to conversions generated by the compiler. Cast-only numeric, object, and array type pairs now classify as `None`; custom IR producers must use `TypeRelations.IsCastable` and `CheckedCast` for identity-preserving runtime conformance.

### New features

- Added warning `MUL3030` when an `is` test is statically known to be false while retaining the expression and its runtime result.
- Added logical-identity tracking to recursive runtime conformance so cyclic object and array graphs can be tested and cast without exhausting the traversal-depth limit.
- Added `TypeRelations.IsCastable` for checking whether two static types permit a runtime-conformance cast.

### Fixes

- Removed redundant runtime-conformance execution for statically guaranteed `as` casts, preventing avoidable execution-budget, cancellation, and traversal failures.

## `0.1.0` → `0.2.0-alpha.1` - 2026-09-24

### Breaking changes

- Changed the default language version from version 1 to version 1.1 for compiler, lexer, parser, profile-builder, and environment-builder APIs. Hosts that require version 1 behavior must now select it explicitly.
- Changed environment fingerprints for schemas containing array signatures because array mutability capability now contributes to type canonicalization. Previously persisted IR using those fingerprints must be recompiled.

### New features

- Added language version 1.1 with covariant read-only array views (`T[]$`) and read-only array literals (`$[...]`).
- Added read-only array common-type inference for conditional expressions, null coalescing, and nested array literals. Compatible array operands may now produce a read-only common type where compilation previously failed.
- Added shape-based array `is` and `as` validation, identity-preserving mutable-to-read-only views, and checked acquisition of mutable capability. `$[...]` values expose only read capability, while readonly views originating from mutable arrays can recover write capability through a successful checked cast.
- Array elements are revalidated against their static element type on every read, and provider arguments are recursively validated before invocation. Shape changes introduced through another mutable alias therefore produce a runtime type error before an invalid value is observed by MuLang code or a provider.
- Read-only provider parameters are source-level contracts. Trusted .NET provider implementations receive the original validated adapter and may observe additional host interfaces implemented by that value.
- Added the independent `IDotNetReadOnlyArrayValue` adapter contract while preserving the existing `IDotNetArrayValue` API for mutable provider arrays.

### Fixes

- Changed language version 1 handling of `$` and `$[` to report that the syntax requires language version 1.1 instead of reporting invalid source characters.

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

- Added the public [`MuLang.IR`](https://fsttesla.github.io/MuLang/api/MuLang.IR.IrProgram.html) model and validation APIs, enabling runtime-independent compilation and custom exporters as defined by the [portable intermediate representation specification](https://fsttesla.github.io/MuLang/ir/portable-intermediate-representation.html).
- Exposed compiler, IR, and .NET runtime diagnostic codes as public constants.
- Exposed `ObjectTypeSymbol.CreateAnonymous`, `TypeSymbols.Null`, and `TypeSymbols.Error` for compiler and IR integrations.

## `0.1.0-alpha.1` → `0.1.0-alpha.2` - 2026-09-18

### Breaking changes

- Changed the numeric value of [`OpenObjectsFeature.Enabled`](https://fsttesla.github.io/MuLang/api/MuLang.Core.OpenObjectsFeature.html) from `1` to `2`.

### New features

- Added [`OpenObjectsFeature.PropertyExistenceOnly`](https://fsttesla.github.io/MuLang/api/MuLang.Core.OpenObjectsFeature.html#MuLang_Core_OpenObjectsFeature_PropertyExistenceOnly), which enables dynamic `has` tests without enabling open-object creation, access, mutation, or provider-declared open types. See the [open-object profile semantics](https://fsttesla.github.io/MuLang/language/14-language-profiles.html#144-open-objects) for details.
