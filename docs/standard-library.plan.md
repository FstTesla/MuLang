# Standard Library Implementation Plan

## Goal

Implement an optional, modular MuLang standard library with:

- runtime-independent declarations in `MuLang.StandardLibrary`;
- matching .NET implementations in `MuLang.StandardLibrary.DotNet`;
- stable provider identifiers;
- explicit host-selected composition;
- no implicit compiler, exporter, or facade imports;
- deterministic collision handling;
- declaration and implementation parity validation.

The standard library is a collection of provider modules, not a privileged language namespace. The compiler resolves its symbols through the ordinary environment schema.

## Dependencies

The initial library depends on the read-only array support implemented for `LanguageVersion.Version2`.

In particular:

- `arrayContains` accepts `unknown?[]$`;
- `objectKeys` returns `string[]$`;
- `objectValues` returns `unknown?[]$`;
- provider implementations need the read-only array runtime contract;
- standard-library declarations require `LanguageVersion.Version2`.

The standard-library projects remain outside the compiler and exporter dependency graph:

- `MuLang.StandardLibrary` depends only on `MuLang.Core`;
- `MuLang.StandardLibrary.DotNet` depends on `MuLang.StandardLibrary` and `MuLang.Exporters.DotNet`;
- no existing production project takes a dependency on either standard-library package.

## Design principles

### Explicit composition

Hosts select individual modules and add them to an environment. No module is imported automatically by the compiler, facade, language profile, or runtime context.

### Flat language namespace

The current MuLang environment has one flat namespace for globals and one flat namespace for functions, with no overloads.

Default standard-library language names must therefore be globally unique across the complete catalog. Module composition also fails explicitly when a standard-library symbol conflicts with a host symbol.

### Stable identifiers

Every declaration has a stable provider identifier using the `mulang.std` prefix.

Identifiers include the module and symbol identity and never depend on registration order or .NET type names.

Renaming a language-visible symbol does not silently change its provider identifier. Any identifier change is a compatibility event.

### Runtime independence

The declarative package contains:

- module metadata;
- types;
- constants and global declarations;
- function signatures;
- composition and validation logic.

It contains no delegates, `System.Random`, `TimeProvider`, GUID generation, text encoders, runtime adapters, or .NET-specific values.

### Runtime parity

Each .NET module declares exactly the globals and functions implemented by its matching declarative module.

Automated parity tests compare:

- module identifiers;
- global identifiers;
- function identifiers;
- argument counts;
- return types;
- implementation presence;
- duplicate registrations.

### Determinism metadata

Modules declare whether they require nondeterministic runtime capabilities.

Initial capability categories are:

- deterministic;
- randomness;
- clock;
- randomness and clock.

This metadata is descriptive and supports host policy. It does not enable modules implicitly.

## Declarative public model

Introduce a small immutable model in `MuLang.StandardLibrary`.

### Standard-library module

A module exposes:

- stable module identifier;
- display name;
- minimum language version;
- capability metadata;
- structured types, if any;
- global declarations;
- function declarations.

Collections are immutable or read-only and have deterministic ordering.

The initial catalog does not require structured types, but the model should support them so future modules do not need a parallel abstraction.

### Module catalog

Expose one canonical module instance for each module.

Also expose read-only catalog collections for discovery. Do not expose an aggregate that silently imports every module.

Convenience collections may group modules by category or determinism, but consumers must still pass the selected modules explicitly to composition.

### Environment composition

Provide a standard-library composer that adds selected module declarations to an `EnvironmentBuilder`.

Composition must:

- require a language version compatible with every module;
- preserve caller-selected module order only for diagnostics, not fingerprints;
- reject duplicate module identifiers;
- reject duplicate type, global, or function names;
- reject duplicate provider identifiers;
- surface the exact conflicting modules and symbols;
- use the ordinary Core environment builder for final schema validation.

Do not create a second environment-schema implementation.

### Stable symbol access

The .NET implementation package must be able to reference declarations without duplicating string identifiers.

Expose declarations through their module objects or through generated/shared immutable symbol descriptors. Avoid parallel manually maintained identifier constants when the declaration itself can be the source of truth.

## .NET implementation model

### Module bindings

Each .NET module provides:

- runtime global values keyed by the declaration identifiers;
- runtime function implementations keyed by the declaration identifiers;
- a reference to the matching declarative module.

Bindings are immutable after creation.

### Runtime composition

Provide a composer for selected .NET module bindings.

It must:

- validate that each implementation matches its declaration;
- reject duplicate global and function identifiers;
- reject implementations for undeclared symbols;
- reject missing implementations;
- expose collections suitable for constructing `DotNetRuntimeContext`;
- compose with host-provided globals and functions while preserving collision errors.

Do not make `DotNetRuntimeContext` depend on the standard-library package.

### Provider invocation services

Some functions require runtime semantics already implemented by the exporter:

- `arrayContains` requires MuLang structural equality, including cyclic arrays and objects;
- object functions require adapter-based property enumeration and reads;
- standard-library runtime errors require consistent MuLang error handling;
- long-running operations should observe cancellation and execution policy.

Introduce a public provider-invocation context in `MuLang.Exporters.DotNet` rather than duplicating runtime operations in the standard library.

The context should provide narrowly scoped services for:

- reading a read-only array;
- reading object property names and values;
- MuLang structural equality;
- cancellation observation;
- reporting a runtime failure at the provider call span.

Update the provider-function delegate contract, or introduce a context-aware delegate alongside the existing one, so standard-library implementations can receive this context.

Prefer one canonical context-aware path after the alpha API transition. Avoid separate semantics for standard-library and host provider functions.

### Configurable nondeterminism

Provide .NET options or services for modules requiring time or randomness.

At minimum:

- Clock uses an injectable `TimeProvider`;
- Random uses an injectable random source with a thread-safe default;
- GUID generation uses runtime facilities while Clock injection controls the timestamp used by Version 7 generation where the platform permits it.

Deterministic modules use singleton bindings. Nondeterministic modules are created from explicit options so tests and hosts can control their dependencies.

## Initial module catalog

Default language names are chosen to avoid collisions in the flat environment. Function names are case-sensitive.

### Math.Constants

| Language name | Type |
|---|---|
| `e` | `float` |
| `pi` | `float` |
| `tau` | `float` |
| `nan` | `float` |
| `positiveInfinity` | `float` |
| `negativeInfinity` | `float` |
| `minInt` | `int` |
| `maxInt` | `int` |

The values use MuLang numeric semantics and invariant .NET representations.

### Math.Basic

| Signature |
|---|
| `abs(value: number): number` |
| `sign(value: number): int` |
| `min(left: number, right: number): number` |
| `max(left: number, right: number): number` |
| `clamp(value: number, minimum: number, maximum: number): number` |

Integer results preserve integer representation. Floating inputs produce floating results where required by the declared `number` semantics.

`abs(minInt)` produces a runtime overflow error. `clamp` rejects an inverted range.

### Math.Rounding

| Signature |
|---|
| `floor(value: number): number` |
| `ceiling(value: number): number` |
| `truncate(value: number): number` |
| `round(value: number): number` |
| `truncateToInt(value: float): int` |

Integer inputs to the `number` functions are returned unchanged.

Floating results remain floating for `floor`, `ceiling`, `truncate`, and `round`.

`round` uses midpoint-to-even.

`truncateToInt` truncates toward zero and produces a runtime error for NaN, infinity, or a result outside the signed 64-bit range.

### Math.Powers

| Signature |
|---|
| `sqrt(value: number): float` |
| `pow(value: number, exponent: number): float` |
| `exp(value: number): float` |
| `log(value: number): float` |
| `log10(value: number): float` |

Results follow IEEE 754 binary64 behavior.

### Math.Trigonometry

| Signature |
|---|
| `sin(value: number): float` |
| `cos(value: number): float` |
| `tan(value: number): float` |
| `asin(value: number): float` |
| `acos(value: number): float` |
| `atan(value: number): float` |
| `atan2(y: number, x: number): float` |
| `degreesToRadians(value: number): float` |
| `radiansToDegrees(value: number): float` |

Angles are radians except where the function name explicitly states a conversion.

### Math.Classification

| Signature |
|---|
| `isFinite(value: number): bool` |
| `isInfinity(value: number): bool` |
| `isNaN(value: number): bool` |

All integer values are finite and are never NaN or infinity.

### Array

| Signature |
|---|
| `arrayContains(array: unknown?[]$, value: unknown?): bool` |

Comparison uses MuLang structural equality, not .NET `Equals`.

The operation is read-only, preserves cycles safely, and returns true for the first structurally equal element.

### Object

| Signature |
|---|
| `objectKeys(obj: object): string[]$` |
| `objectValues(obj: object): unknown?[]$` |

Only currently visible properties are included. Absent optional properties are excluded; present properties with null values are included.

Keys are sorted using ordinal order to make results deterministic across adapters. Values use the corresponding sorted-key order.

Returned arrays are read-only.

### String.Inspection

| Signature |
|---|
| `stringLength(value: string): int` |
| `charAt(value: string, index: int): int` |
| `isEmpty(value: string): bool` |
| `isWhiteSpace(value: string): bool` |
| `stringContains(value: string, part: string): bool` |
| `startsWith(value: string, prefix: string): bool` |
| `endsWith(value: string, suffix: string): bool` |

String indexes and lengths count Unicode scalar values.

`charAt` returns the Unicode scalar value and produces a runtime error for a negative or out-of-range index.

Search operations are ordinal and culture-independent.

### String.Search

| Signature |
|---|
| `indexOf(value: string, part: string): int?` |
| `lastIndexOf(value: string, part: string): int?` |

Returned indexes count Unicode scalar values. No match produces null.

Search is ordinal and culture-independent.

### String.Transform

| Signature |
|---|
| `toLower(value: string): string` |
| `toUpper(value: string): string` |
| `trim(value: string): string` |
| `trimStart(value: string): string` |
| `trimEnd(value: string): string` |
| `repeat(value: string, count: int): string` |
| `reverse(value: string): string` |

Case conversion is Unicode-invariant and does not use the process culture.

`reverse` operates on Unicode scalar values.

`repeat` rejects negative counts and reports overflow or allocation failures as MuLang runtime errors.

### String.Slicing

| Signature |
|---|
| `substring(value: string, start: int, length: int): string` |
| `remove(value: string, start: int, length: int): string` |
| `insert(value: string, index: int, inserted: string): string` |

Indexes and lengths count Unicode scalar values.

Negative values and ranges outside the scalar length produce runtime errors.

### String.Replacement

| Signature |
|---|
| `replaceFirst(value: string, oldValue: string, newValue: string): string` |
| `replaceAll(value: string, oldValue: string, newValue: string): string` |

Matching is ordinal and non-overlapping.

An empty `oldValue` is rejected to avoid insertion semantics that differ across runtimes.

### String.Comparison

| Signature |
|---|
| `compareOrdinal(left: string, right: string): int` |
| `compareIgnoreCase(left: string, right: string): int` |
| `equalsIgnoreCase(left: string, right: string): bool` |

Comparison results are normalized to `-1`, `0`, or `1`.

Ignore-case operations use Unicode invariant case comparison and never use the process culture.

### Parsing

| Signature |
|---|
| `parseInt(value: string): int?` |
| `parseFloat(value: string): float?` |
| `parseBool(value: string): bool?` |

Parsing is invariant, consumes the complete input, and does not ignore leading or trailing whitespace.

Integer and finite float forms follow MuLang source numeric syntax. Float parsing additionally accepts the runtime spellings `NaN`, `Infinity`, and `-Infinity`.

Boolean parsing accepts only `true` and `false`.

Invalid or out-of-range input produces null.

### Random

| Signature |
|---|
| `randomFloat(): float` |
| `randomInt(minimum: int, maximum: int): int` |

`randomFloat` returns a value in `[0, 1)`.

`randomInt` returns a value in `[minimum, maximum)` and rejects empty or inverted ranges.

The module is not cryptographic and is marked as requiring randomness.

### Clock

| Signature |
|---|
| `unixTimeSeconds(): int` |
| `unixTimeMilliseconds(): int` |

Values represent UTC Unix time in signed 64-bit units.

The module is marked as requiring a clock.

### Guid

| Signature |
|---|
| `newGuid(): string` |
| `newGuidV7(): string` |
| `isGuid(value: string): bool` |

`newGuid` produces a Version 4 GUID.

`newGuidV7` produces a Version 7 GUID using the configured clock.

Generated values use lowercase canonical `D` format. `isGuid` accepts canonical `D` format case-insensitively and rejects alternate GUID formats.

The module is marked as requiring randomness and clock access.

### Text.Encoding

| Signature |
|---|
| `base64Encode(value: string): string` |
| `base64Decode(value: string): string?` |

Encoding converts the input string to UTF-8 and then to standard padded Base64.

Decoding requires valid standard Base64 and valid UTF-8. Invalid input produces null.

## Error semantics

Use nullable results for expected parse or validation failure where the signature declares a nullable result.

Use MuLang runtime errors for:

- invalid indexes or ranges;
- arithmetic overflow;
- invalid arguments to totality-constrained operations;
- impossible conversions;
- runtime resource failures;
- adapter contract violations.

Do not silently substitute defaults or return null from non-nullable signatures.

Define standard-library runtime error codes in a stable namespace. The .NET package maps platform exceptions to these codes without exposing platform-specific exception behavior.

## Resource and execution behavior

Every standard-library call already consumes the provider-call execution budget.

Functions that traverse arrays, objects, or long strings must:

- observe cancellation through the provider invocation context;
- use checked length and allocation calculations;
- avoid recursion where iteration is sufficient;
- use the runtime traversal-depth policy for structural equality;
- avoid culture-sensitive global state.

If per-element budget charging is introduced, it should be a general provider-operation facility in the exporter rather than a standard-library-only counter.

## Testing strategy

### Declarative package tests

Cover:

- module metadata;
- stable identifiers;
- minimum language versions;
- exact symbol names and signatures;
- global uniqueness across the complete catalog;
- deterministic ordering;
- collision diagnostics;
- selective composition;
- composition with host declarations;
- environment fingerprints;
- absence of implicit imports.

### .NET parity tests

For every module, verify:

- one matching implementation binding;
- no missing globals or functions;
- no extra implementations;
- exact identifier matching;
- correct global runtime types;
- correct function result runtime types;
- compatibility with the module environment fingerprint.

### Function semantics tests

Cover normal, boundary, nullable, invalid, overflow, Unicode, NaN, infinity, cyclic, adapter, and cancellation cases as applicable.

String tests must include Unicode scalars outside the BMP and combining sequences without assuming grapheme-cluster semantics.

Array and object tests must use both internal values and public runtime adapters.

Random tests validate ranges and injected deterministic sources without asserting distribution from a small sample.

Clock tests use an injected `TimeProvider`.

GUID tests validate version, canonical formatting, and Version 7 timestamp behavior without depending on ambient wall-clock time.

### Integration tests

Compile and execute source against:

- one selected module;
- multiple non-conflicting modules;
- host declarations plus selected modules;
- declaration-only environments missing implementations;
- runtime bindings with missing or duplicate entries;
- Version 1 environments attempting to use Version 2 modules;
- read-only array arguments and results.

## Documentation and packaging

Update package READMEs and conceptual documentation with:

- package selection;
- explicit module composition;
- declaration/runtime package separation;
- module catalog;
- stable identifier policy;
- deterministic and nondeterministic module metadata;
- Version 2 requirement;
- runtime service configuration;
- collision behavior;
- no implicit compiler or facade integration.

Update DocFX inputs and navigation for both standard-library assemblies.

Populate fresh public API baselines for both packages and record the transition from placeholders to functional packages in the changelog.

Package verification must continue validating:

- `MuLang.StandardLibrary` depends only on `MuLang.Core`;
- `MuLang.StandardLibrary.DotNet` depends on the declarations and .NET exporter packages;
- neither package is introduced as a dependency of compiler or facade packages;
- symbol packages and Source Link are present.

## Implementation phases

### Phase 1: Finalize contracts and semantics

1. Finalize module, composer, capability, and binding type names.
2. Freeze stable module and symbol identifiers.
3. Confirm every language-visible name is globally unique.
4. Encode the semantic rules from this plan in focused specification text.
5. Confirm the provider invocation context required by array and object functions.

Exit criteria:

- every initial symbol has one signature, identifier, module, and semantic definition;
- no module collision exists in the complete catalog;
- runtime services required by implementations are explicit.

### Phase 2: Validate read-only array prerequisite

1. Verify `unknown?[]$`, `string[]$`, and read-only runtime adapters.
2. Verify context-aware provider calls and structural equality access.

Exit criteria:

- declaration signatures can represent all initial array functions;
- provider implementations can consume arrays declared read-only through the runtime adapter contracts.

### Phase 3: Build declarative module infrastructure

1. Add immutable module and capability models.
2. Add module composition and collision validation.
3. Add stable declaration access for runtime packages.
4. Add the complete declaration catalog.
5. Add declarative package tests.

Exit criteria:

- hosts can compose any selected module set into an environment;
- declarations contain no runtime-specific dependency;
- catalog-wide uniqueness and fingerprints are tested.

### Phase 4: Extend .NET provider invocation

1. Add the context-aware provider delegate path.
2. Expose structural equality and adapter reads through a narrow context.
3. Add consistent standard-library runtime error creation.
4. Add cancellation observation.
5. Update exporter tests and public API baselines.

Exit criteria:

- standard-library implementations do not duplicate exporter runtime semantics;
- host provider functions can use the same supported invocation model.

### Phase 5: Build .NET composition infrastructure

1. Add immutable module bindings.
2. Add declaration/implementation parity validation.
3. Add runtime composition with host values and functions.
4. Add injectable time and randomness services.
5. Add parity and collision tests.

Exit criteria:

- selected declaration modules and runtime bindings compose independently but validate as one set;
- missing, duplicate, and extra implementations fail explicitly.

### Phase 6: Implement deterministic modules

Implement and test:

- Math modules;
- Array;
- Object;
- String modules;
- Parsing;
- Text.Encoding;
- GUID validation.

Exit criteria:

- deterministic modules pass semantic, adapter, Unicode, and integration tests;
- no implementation uses process culture.

### Phase 7: Implement nondeterministic modules

Implement and test:

- Random;
- Clock;
- GUID generation.

Exit criteria:

- runtime dependencies are injectable;
- tests do not depend on ambient time or uncontrolled randomness;
- module capability metadata is correct.

### Phase 8: Documentation, packaging, and release integration

1. Update package READMEs and conceptual documentation.
2. Add DocFX API coverage.
3. Populate shipped public API baselines.
4. Update changelogs and package descriptions.
5. Run package dependency and content verification.
6. Run full Debug and Release validation.

Exit criteria:

- both packages are functional, documented, packable, and verifiable;
- consumers can select declarations and matching .NET bindings without implicit imports.

## Final acceptance criteria

- The standard library is optional and explicitly composed.
- Declarative modules remain runtime-independent.
- .NET implementations match declarations exactly.
- Default language names are globally unique.
- Stable provider identifiers are documented and tested.
- Collisions fail explicitly.
- No compiler, facade, or exporter package depends on the standard library.
- `arrayContains`, `objectKeys`, and `objectValues` use read-only arrays.
- String operations use Unicode scalar indexing where specified.
- Numeric operations follow MuLang numeric and overflow semantics.
- Random, Clock, and GUID generation use explicit runtime capabilities.
- Culture does not affect deterministic results.
- Runtime errors and nullable failures follow their declared contracts.
- Documentation, public API baselines, packaging, and release verification cover both packages.
