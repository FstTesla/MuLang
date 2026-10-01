# Standard library

MuLang provides an optional standard library as explicitly selected provider
modules. It is not part of the language syntax, is not privileged by the
compiler, and is never imported automatically.

## Packages

`MuLang.StandardLibrary` contains runtime-independent declarations, metadata,
the canonical module catalog, and environment composition.

`MuLang.StandardLibrary.DotNet` contains matching .NET provider bindings,
runtime composition, deterministic implementations, and configurable runtime
services for nondeterministic modules.

The declaration package depends only on `MuLang.Core`. The .NET package depends
on the declaration package and `MuLang.Exporters.DotNet`. Compiler and facade
packages do not depend on either standard-library package.

## Explicit composition

Hosts select declaration modules from `StandardLibraryCatalog` and pass them to
`StandardLibraryComposer`. The resulting environment can be used directly or
combined with a host environment. The Array and Object modules require language
version 1.1 because their signatures expose read-only arrays. Every other
initial module supports language version 1.

.NET hosts select the matching bindings from
`DotNetStandardLibraryModules` and combine them through
`DotNetStandardLibraryComposer`. The resulting globals and provider functions
are suitable for `DotNetRuntimeContext.Create`.

Declaration and runtime composition are independent so alternative runtimes
can implement the same declarations. The .NET composer validates declaration
and implementation parity before exposing a composition.

## Module catalog

| Module | Capability | Language symbols |
|---|---|---|
| Math.Constants | Deterministic | `e`, `pi`, `tau`, `minInt`, `maxInt` |
| Math.Basic | Deterministic | `abs`, `sign`, `min`, `max`, `clamp` |
| Math.Rounding | Deterministic | `floor`, `ceiling`, `truncate`, `round`, `truncateToInt` |
| Math.Powers | Deterministic | `sqrt`, `pow`, `exp`, `log`, `log10` |
| Math.Trigonometry | Deterministic | `sin`, `cos`, `tan`, `asin`, `acos`, `atan`, `atan2`, `degreesToRadians`, `radiansToDegrees` |
| Math.Classification | Deterministic | `isFinite`, `isInfinity`, `isNaN` |
| Array | Deterministic | `arrayContains` |
| Object | Deterministic | `objectKeys`, `objectValues` |
| String.Inspection | Deterministic | `stringLength`, `charAt`, `isEmpty`, `isWhiteSpace`, `stringContains`, `startsWith`, `endsWith` |
| String.Search | Deterministic | `indexOf`, `lastIndexOf` |
| String.Transform | Deterministic | `toLower`, `toUpper`, `trim`, `trimStart`, `trimEnd`, `repeat`, `reverse` |
| String.Slicing | Deterministic | `substring`, `remove`, `insert` |
| String.Replacement | Deterministic | `replaceFirst`, `replaceAll` |
| String.Comparison | Deterministic | `compareOrdinal`, `compareIgnoreCase`, `equalsIgnoreCase` |
| Parsing | Deterministic | `parseInt`, `parseFloat`, `parseBool` |
| Text.Encoding | Deterministic | `base64Encode`, `base64Decode` |
| Random | Randomness | `randomFloat`, `randomInt` |
| Clock | Clock | `unixTimeSeconds`, `unixTimeMilliseconds` |
| Guid | Randomness and clock | `newGuid`, `newGuidV7`, `isGuid` |

The catalog exposes canonical module instances and a read-only `All`
collection for discovery. There is no aggregate operation that imports every
module.

## Stable identifiers

Module identifiers use the `mulang.std.<module>` form. Global identifiers use
`mulang.std.<module>.global.<name>`, and function identifiers use
`mulang.std.<module>.function.<name>`.

The canonical declaration objects are the source of truth for these
identifiers. .NET bindings reference the declarations rather than maintaining
a second identifier catalog. Identifier changes are compatibility changes.

## Determinism and runtime services

Each module declares whether it is deterministic or requires randomness,
clock access, or both. Capability metadata is descriptive and does not enable a
module automatically.

Deterministic .NET bindings are immutable shared instances. Random bindings
accept `RandomStandardLibraryOptions` with an optional
`IStandardLibraryRandomSource`. Clock bindings accept
`ClockStandardLibraryOptions` with a `TimeProvider`. GUID bindings accept
`GuidStandardLibraryOptions` with a `TimeProvider`; GUID entropy remains
platform-provided.

The defaults use a thread-safe non-cryptographic random source and
`TimeProvider.System`. Hosts that require repeatable tests or controlled time
must supply their own services.

## Runtime semantics

Numeric functions preserve MuLang integer representation where specified and
report invalid ranges, overflow, and impossible conversions as application
runtime errors. Deterministic parsing, formatting, comparison, and encoding do
not use the process culture.

String indexes and lengths count Unicode scalar values. Search and replacement
are ordinal. Object keys use ordinal sorting, and object values follow the same
key order. Object and array results are read-only.

`arrayContains` uses MuLang structural equality, including cyclic arrays and
objects. Array and object access uses the canonical provider invocation context,
so adapters, cancellation, traversal limits, and execution budgets retain
exporter semantics.

Expected parse, validation, and decoding failures return `null` where the
declaration is nullable. Non-nullable operations do not substitute defaults.

## Validation and collisions

Declaration composition rejects incompatible language versions, duplicate
module identifiers, duplicate language names, duplicate provider identifiers,
and conflicts with host declarations. Diagnostics identify the conflicting
modules and symbols.

.NET composition rejects duplicate runtime identifiers, missing
implementations, implementations for undeclared symbols, argument-count
mismatches, and collisions with host globals or functions.
