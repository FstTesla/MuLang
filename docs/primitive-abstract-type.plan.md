# `primitive` abstract type plan

## Status

Proposed for MuLang `1.2`.

## Goal

Introduce `primitive` as the common non-null abstract type of every MuLang
primitive value.

The type should:

- represent `bool`, `int`, `float`, `number`, and `string` values;
- retain the concrete runtime representation of every value;
- support contextual conversion to `string` during string concatenation;
- participate in ordinary assignability, nullability, type tests, checked
  casts, common-type inference, and read-only array views.

## Type model

`primitive` is an intrinsic abstract type with no literal syntax and no
dedicated runtime representation.

Its direct conceptual subtypes are:

- `bool`;
- `number`;
- `string`.

`int` and `float` remain subtypes of `number` and are therefore transitively
assignable to `primitive`.

`primitive` does not include:

- `null`;
- objects or structured objects;
- arrays;
- `void`;
- compiler error-recovery values;
- future non-primitive intrinsic types such as `error`.

`primitive?` includes `null` through the existing nullable-type construction.
Primitive values remain immutable.

The hierarchy is:

1. concrete primitive types;
2. `number` for the numeric branch;
3. `primitive`;
4. `unknown`.

## Assignability and conversions

Add these implicit widening relations:

- `bool`, `int`, `float`, `number`, and `string` to `primitive`;
- `primitive` to `unknown`;
- the corresponding nullable lifting and read-only array-view relations.

No widening from `object`, a structured object, or an array to `primitive` is
permitted.

Checked casts and type tests should support:

- refinement from `primitive` to any concrete primitive type or `number`;
- refinement from `unknown` to `primitive`;
- widening to `primitive`, which is statically guaranteed for a primitive
  source;
- nullable removal and refinement through the existing nullable rules.

Checked casts preserve the original runtime representation. In particular,
passing through `primitive` must not perform numeric promotion or contextual
string conversion.

Runtime conformance to `primitive` succeeds only for Boolean, integer,
floating-point, and string runtime values.

## Common-type inference

Preserve the existing numeric common-type rules before considering
`primitive`.

When two non-null primitive types have no more specific common type, their
common type is `primitive`. This applies to:

- conditional expressions;
- null-coalescing expressions;
- array-literal element inference;
- other compiler surfaces using `TypeRelations.GetCommonType`.

Nullable operands produce `primitive?` through the existing lifting rules.

Mixed primitive array literals may consequently infer `primitive[]`.
Mutable arrays remain invariant. Read-only array compatibility permits a
primitive element type to widen to `primitive`.

No common type between a primitive value and an object or array is inferred
solely through `primitive`; `unknown` remains the explicit universal
non-null supertype.

## Operators

`primitive` supports the equality, identity, and truthiness behavior already
defined for values whose concrete representation is inspected at runtime.

String concatenation is valid when:

- one operand has static type `string`; and
- the other operand has type `primitive`, `primitive?`, another primitive
  type, a nullable primitive type, or the null literal.

The non-string operand uses the existing culture-independent primitive string
conversion. A nullable operand containing `null` produces the existing null
representation.

Two operands whose static type is only `primitive` do not trigger dynamic
selection between arithmetic and concatenation. Arithmetic, relational,
bitwise, and unary primitive operators continue requiring their existing
specific operand types.

Objects and arrays remain unavailable to intrinsic string concatenation.

## Language version and keyword compatibility

Reserve `primitive` as a keyword starting with MuLang `1.2`.

Language versions 1 and 1.1 continue tokenizing it as an identifier and emit
the existing future-reserved-keyword warning once `1.2` is supported.

Environment validation remains version-aware:

- `primitive` may remain a provider symbol name for schemas targeting an
  earlier language version;
- schemas targeting `1.2` reject it as a provider type, global, function, or
  parameter name.

Add `primitive` to lexical classification, TextMate grammar, and every
language-version-aware keyword table.

## Language-version availability

The availability of `primitive` is determined exclusively by the selected
language version. Do not introduce a dedicated language-profile feature flag.

MuLang `1.2` and its standard profile expose the type. Earlier language
versions continue treating `primitive` as an identifier and cannot construct
the type through source syntax.

Environment schemas may contain `primitive`, including within nullable types,
arrays, and object properties, only when they target MuLang `1.2` or later.
No independent profile setting is required.

## Core type-system changes

Add a new `TypeKind.Primitive` value without renumbering existing enum values.

Expose `TypeSymbols.Primitive` as the canonical intrinsic symbol.

Update:

- equivalence and display names;
- assignability and conversion classification;
- checked-cast availability and guaranteed conformance;
- common-type inference;
- read-only array-view compatibility;
- environment validation and fingerprints;
- object-type graph construction and canonicalization.

The current internal primitive-operation helpers use a broader meaning of
"primitive" that includes `unknown` and `null`. Split or rename those helpers
so the source-level `primitive` type has one precise definition and
constant-evaluation support retains any broader operational classification it
requires.

## Compiler changes

Add a versioned `primitive` keyword token and recognize it as a primary type.

Update the binder to:

- resolve the keyword to `TypeSymbols.Primitive`;
- apply the new assignability, common-type, cast, type-test, and concatenation
  rules;
- preserve more specific numeric result types;
- avoid enabling arithmetic or other concrete operators on an abstract
  primitive operand.

Update lowering and constant folding so a value with static type `primitive`
retains its concrete constant or runtime representation.

Update semantic classification so `primitive` is reported as an intrinsic
type keyword.

## Portable IR and MuIR

Represent `primitive` as an intrinsic portable IR type. No new instruction or
runtime value representation is required.

Update IR validation for:

- slot, return, provider, and user-function types;
- constants carrying a concrete value under static type `primitive`;
- conversions and checked casts;
- type tests;
- operator operand and result validation;
- truthiness normalization.

Adding a required intrinsic type token is incompatible with MuIR version 1.
Serialize `primitive` only in the next MuIR format version and coordinate that
version with scoped slots and exception-handling regions as recorded in the
roadmap.

The MuIR reader and writer must:

- use a stable `primitive` intrinsic-type token;
- include it in deterministic type-table canonicalization;
- reconstruct the canonical `TypeSymbols.Primitive` symbol;
- continue reading version 1 documents without synthesizing or inferring the
  new type;
- reject the new token when reading a version 1 document.

## .NET exporter and runtime

The exporter should represent a `primitive` slot with the same general runtime
storage used for other abstract types.

Runtime conformance recognizes only:

- `bool`;
- `long`;
- `double`;
- `string`.

Conversions to `primitive` preserve the value unchanged. Checked refinement
from `primitive` uses the existing concrete runtime checks.

String concatenation dispatches through the existing primitive value
operations and culture-independent formatting. No boxing wrapper, tagged
union, or new provider adapter is introduced.

Provider arguments, globals, return values, arrays, and object properties
declared as `primitive` must be recursively validated at the same boundaries
as other abstract types.

## Standard library

Standard-library declarations may use `primitive` only when their minimum
language version and composed environment target MuLang `1.2` or later.

The type may be useful for formatting, inspection, and collection operations,
but its introduction does not require adding any standard-library function.

## Test plan

Add Core tests covering:

- canonical type identity and display name;
- complete positive and negative assignability matrices;
- nullable lifting;
- checked-cast and type-test relations;
- guaranteed conformance;
- common-type inference;
- mutable-array invariance and read-only array covariance;
- environment fingerprint changes.

Add compiler tests covering:

- versioned keyword behavior and migration warnings;
- declarations, parameters, returns, arrays, and properties;
- conditional, coalescing, and array-literal common types;
- string concatenation in both operand orders;
- rejection of unsupported arithmetic and other operators;
- checked casts and type tests;
- constant folding and semantic classification.

Add IR and MuIR tests covering:

- every valid use of the intrinsic type;
- constant-value validation;
- conversion and type-test validation;
- deterministic serialization in the new format;
- rejection by the version 1 reader;
- compatibility when reading existing version 1 fixtures.

Add exporter tests covering:

- every concrete primitive runtime representation;
- nullable values;
- concatenation;
- successful and failed checked refinement;
- provider boundary validation;
- arrays and object properties containing mixed primitive values.

Update public API baselines after the behavior is complete.

## Documentation updates

Update:

- the type hierarchy and primitive-type specification;
- assignability and conversion rules;
- operator rules;
- language profiles;
- lexical structure and grammar summary;
- portable IR and MuIR specifications;
- Visual Studio language support;
- package/API documentation;
- the detailed changelog.

Document explicitly that `primitive` is narrower than `unknown`, has no
dedicated runtime representation, and does not enable dynamic arithmetic.

## Implementation sequence

1. Finalize the semantic hierarchy and language-version behavior.
2. Add the Core type symbol, relations, environment validation, and fingerprints.
3. Add versioned keyword handling, parser support, binding, and diagnostics.
4. Implement common-type inference and string-concatenation behavior.
5. Update constant folding and primitive runtime operations.
6. Add portable IR support and validation.
7. Add .NET exporter conformance and provider-boundary support.
8. Add the new MuIR type token with the coordinated format-version changes.
9. Update editor classification and grammar assets.
10. Complete tests, specifications, API baselines, and changelog.
