# Standard library

MuLang provides an optional standard library as explicitly selected provider
symbols. It is not part of the language syntax, is not privileged by the
compiler, and is never imported automatically.

## Packages

`MuLang.StandardLibrary` contains runtime-independent symbol declarations,
metadata, proposed modules, normalized selections, and environment composition.

`MuLang.StandardLibrary.DotNet` binds those selections to .NET provider values
and functions and configures runtime services for nondeterministic symbols.

The declaration package depends only on `MuLang.Core`. The .NET package depends
on the declaration package and `MuLang.Exporters.DotNet`. Compiler and facade
packages do not depend on either standard-library package.

## Explicit composition

Hosts select canonical symbols through `StandardLibraryCatalog.Types`,
`Globals`, and `Functions`, or expand proposed groups from
`StandardLibraryCatalog.Modules`. `StandardLibrarySelection` normalizes the
chosen symbols, expands dependencies, removes duplicates, and derives their
minimum language version and runtime capabilities.

`StandardLibraryComposer` creates an environment from the selection, optionally
combined with a completed host environment. A configured
`DotNetStandardLibrary` binds the same selection to immutable runtime globals
and provider functions suitable for `DotNetRuntimeContext.Create`.

Alternative runtimes can bind the same selection independently. The .NET
package validates catalog-wide declaration and implementation parity.

## Module catalog

| Module | Capability | Language symbols |
|---|---|---|
| Math.Constants | Deterministic | `e`, `pi`, `tau`, `minInt`, `maxInt` |
| Math.Basic | Deterministic | `abs`, `sign`, `min`, `max`, `clamp` |
| Math.Rounding | Deterministic | `floor`, `ceil`, `trunc`, `round`, `truncToInt` |
| Math.Powers | Deterministic | `sqrt`, `pow`, `exp`, `log`, `log10` |
| Math.Trigonometry | Deterministic | `sin`, `cos`, `tan`, `asin`, `acos`, `atan`, `atan2`, `degToRad`, `radToDeg` |
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

The catalog exposes one canonical property for every symbol and proposed
module. Each nested `Types`, `Globals`, `Functions`, and `Modules` catalog also
provides `All`, `TryGetById`, and ordinal case-sensitive `TryGetByName`.

Modules reference the same canonical symbols exposed by the typed catalogs.
Their minimum language version is the maximum required by their members, and
their capability is the union of member capabilities. Selecting `isGuid`
alone is deterministic, while the complete Guid module requires randomness and
clock access.

## Stable identifiers

Module identifiers use the `mulang.std.<module>` form. Global identifiers use
`mulang.std.<module>.global.<name>`, and function identifiers use
`mulang.std.<module>.function.<name>`.

Canonical symbol objects are the source of truth for these identifiers. The
.NET implementation registry binds those declarations without maintaining a
parallel public identifier catalog. Identifier changes are compatibility
changes.

## Determinism and runtime services

Each symbol declares whether it is deterministic or requires randomness, clock
access, or both. A module and selection derive their capability from the union
of their symbols. Capability metadata is descriptive and does not enable a
symbol automatically.

`DotNetStandardLibrary` accepts `RandomStandardLibraryOptions`,
`ClockStandardLibraryOptions`, and `GuidStandardLibraryOptions`. Random options
can provide an `IStandardLibraryRandomSource`; clock and GUID options provide a
`TimeProvider`. GUID entropy remains platform-provided.

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

Selection rejects conflicting provider identifiers and dependency cycles.
Declaration composition rejects incompatible language versions, duplicate
language names, duplicate provider identifiers, and conflicts with host
declarations. Diagnostics identify the conflicting symbols.

.NET binding rejects unsupported or noncanonical symbols. Runtime identifier
collisions with host values and functions remain enforced by
`DotNetRuntimeContext.Create`.
