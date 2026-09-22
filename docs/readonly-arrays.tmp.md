# Read-Only Arrays Implementation Plan

## Goal

Introduce read-only array views as an official MuLang type-system concept.

The feature provides:

- the postfix type modifier `$`;
- the read-only array type syntax `T[]$`;
- the read-only array literal opener `$[`;
- safe covariance for read-only array views;
- static prevention of element mutation through a read-only view;
- runtime contracts that allow genuinely read-only provider arrays;
- a reusable foundation for read-only capabilities on future type constructions.

Mutable arrays remain invariant. Read-only arrays are views, not deeply immutable values.

## Decisions

| Concern | Decision |
|---|---|
| Type syntax | `T[]$` |
| Literal syntax | `$[value1, value2]` |
| Literal opener | `$[` is one lexical token |
| Modifier meaning | `$` removes write capability from the immediately preceding supported type construction |
| Initial supported construction | Arrays only |
| Mutability | Determined by the static type through which a value is accessed |
| Depth | Shallow; contained objects and arrays retain their own capabilities |
| Storage | No copy is required when creating a read-only view |
| Identity | Mutable array and read-only views preserve the same logical identity |
| Mutable array variance | Invariant |
| Read-only array variance | Covariant under a dedicated representation-safe relation |
| Mutable-to-read-only conversion | Implicit when the element types are view-compatible |
| Read-only-to-mutable conversion | Never implicit; permitted through `as` when the runtime value is writable and its current elements conform structurally |
| Runtime array typing | Shape-based; array casts do not require a reified nominal element type |
| Runtime conformance | `is` and `as` validate the current capability and elements; they do not establish a permanent invariant across aliases |
| Literal inference | `$[...]` infers the element type using the existing array-literal rules |
| Empty literal | `$[]` requires an expected read-only array type |
| Versioning | Introduce with `LanguageVersion.Version2`; Version 1 rejects the feature |
| Feature flag | None; it is part of the Version 2 type system |

## Syntax and binding

### Type grammar

Extend type suffix parsing with `$`.

The canonical order is:

1. element type;
2. `[]`;
3. optional `$`;
4. optional `?`.

Consequently:

| Type | Meaning |
|---|---|
| `T[]` | Mutable array of non-null `T` elements |
| `T[]$` | Read-only view of an array of non-null `T` elements |
| `T?[]$` | Read-only view whose elements may be null |
| `T[]$?` | Nullable read-only array view |
| `T?[]$?` | Nullable read-only view whose elements may be null |

`T[]?$` is non-canonical and invalid. Repeated `$` modifiers are invalid.

Nested constructions bind from left to right. Each `$` qualifies only the array construction immediately before it.

The parser must retain enough suffix structure to distinguish element nullability, array read-only capability, outer array construction, and array nullability without reconstructing meaning from token text.

### Literal grammar

Add a read-only array literal form using the single `$[` token and the existing `]` closer.

The literal follows the existing array-literal rules for:

- element evaluation order;
- common-type inference;
- expected element types;
- trailing commas;
- empty-literal diagnostics;
- conversion of elements to the inferred or expected element type.

The only type-system difference is that the resulting array type has read-only capability.

A read-only literal must not become mutable because it appears in a mutable expected context. Assigning `$[...]` to `T[]` is invalid.

A normal mutable literal may be used in a read-only expected context through the ordinary mutable-to-read-only conversion. The allocated value remains a mutable array observed through a read-only view.

### Static operations

Read-only arrays support:

- element reads;
- optional element reads;
- the intrinsic `length` property;
- equality and identity operations;
- type tests;
- explicit checked conversions, including acquisition of write capability when runtime validation succeeds;
- passage to compatible read-only parameters and return types.

Read-only arrays do not support element assignment.

The assignment diagnostic must be specific to read-only capability rather than reporting an unrelated type mismatch or disabled mutation feature.

The language profile mutation flags continue to control writes through mutable array types. A read-only target is invalid independently of the selected mutation profile.

## Type model

### Array representation

Keep one public `ArrayTypeSymbol` abstraction and add an `IsReadOnly` property.

Retain `TypeKind.Array`; read-only capability is a property of the array construction, not a new runtime value category.

Add factories that make capability explicit:

- `TypeSymbols.Array(TypeSymbol elementType)` for mutable arrays;
- `TypeSymbols.ReadOnlyArray(TypeSymbol elementType)` for read-only views.

`ArrayTypeSymbol.DisplayName` must append `$` after `[]` when `IsReadOnly` is true.

Array type construction must reject invalid element types exactly as it does today.

### Equivalence

Array types are equivalent only when:

- both have the same read-only capability; and
- their element types are equivalent.

Therefore `T[]` and `T[]$` are not equivalent even when they can share the same runtime value.

### View compatibility

Introduce a dedicated relation for determining whether values can be observed through a read-only array element type.

This relation must be narrower than general implicit conversion because a view cannot transform stored elements on every read.

The initial relation should permit:

- equivalent types;
- non-null types to `unknown`;
- all value types to `unknown?`;
- nullable lifting when the underlying types are view-compatible;
- `int` and `float` to `number`;
- structured object types to `object`;
- recursively compatible read-only array views;
- mutable nested arrays to compatible read-only nested array views.

It must not permit representation-changing conversions, including `int` to `float`.

Structured object compatibility remains governed by the existing invariant structural rules because read-only array capability does not make contained objects read-only.

Expose the relation publicly only if hosts or future modules need to construct or inspect compatible signatures. Otherwise keep it as an implementation detail behind the supported assignment APIs.

### Assignability

Apply these rules:

- `S[]` is assignable to `T[]` only when the array types are equivalent;
- `S[]` is assignable to `T[]$` when `S` is view-compatible with `T`;
- `S[]$` is assignable to `T[]$` when `S` is view-compatible with `T`;
- `S[]$` is never implicitly assignable to `T[]`;
- nullability is applied after the array capability rules.

The mutable-to-read-only conversion is an ordinary implicit conversion and requires no runtime check or allocation.

### Common types

Preserve a mutable common array type only when both operands have equivalent mutable array types.

When a common type is required between compatible mutable and read-only arrays, select the least read-only array view that can represent both element types.

Do not infer a covariant mutable array type.

Conditional expressions, null coalescing, array literals containing arrays, and return-type validation must all use the same common-type rules.

### Checked conversions and type tests

Array type tests and checked conversions are shape-based. They inspect the runtime value presented to the operation and do not require a nominal or reified runtime element-type declaration.

`is T[]` evaluates to true only when:

- the runtime value exposes mutable array capability; and
- every current element conforms recursively to `T`.

`value as T[]` performs the same validation. On success it returns the same logical array with static type `T[]`; otherwise it produces a checked-conversion runtime error.

`is T[]$` evaluates to true when the runtime value exposes read capability and every current element conforms recursively to `T`. It does not require mutable capability.

`value as T[]$` performs the same validation and returns an identity-preserving read-only view.

Consequently, `T[]$ as T[]` is permitted but succeeds only when the runtime value presented to the cast exposes mutable capability and its current elements conform to `T`. A genuinely read-only adapter or a read-only projection that intentionally hides mutation capability fails the cast even if another object elsewhere wraps the same underlying storage mutably.

Successful `is` or `as` validation describes the array at that instant. It does not establish a permanent element invariant across aliases. A different mutable alias may later place a nonconforming value into the array.

Every element read through a statically typed array must therefore validate the retrieved runtime value against the expected element type. If another alias has invalidated the shape, the read produces a runtime type error.

Every element write remains statically checked against the target array element type, and the runtime adapter may still reject the mutation.

## Lexing and parsing

### Tokens

Add:

- a standalone `$` token for type suffixes;
- a `$[` token for read-only array literals.

The lexer must recognize `$[` before standalone `$`.

Recognizing these tokens does not reserve every future sequence beginning with `$`. Future tokens such as interpolated-string openers can still use longest-match tokenization.

### Version diagnostics

Version 2 parses and binds the new syntax.

Version 1 should report a language-version diagnostic for `$` and `$[` rather than treating them as permanently invalid characters. This keeps lexical recognition stable across language versions.

### Syntax model

Update `TypeSyntax` handling so capability suffixes are represented and validated in source order.

Update `ArrayLiteralExpressionSyntax` to expose whether the opener is mutable `[` or read-only `$[`.

Keep a shared parser path for mutable and read-only literals to avoid duplicating element and comma handling.

## Compiler changes

### Binder

Update the binder to:

- construct mutable or read-only array types from type syntax;
- infer `$[...]` as a read-only array;
- use expected read-only element types for empty and contextually typed literals;
- insert implicit mutable-to-read-only conversions;
- classify read-only-to-mutable array conversions as checked when runtime validation can establish writable capability and element conformance;
- reject element assignments through read-only targets;
- apply read-only covariance to arguments, returns, locals, conditionals, and null coalescing;
- preserve the existing nullable-array behavior;
- produce targeted diagnostics for invalid `$` placement and capability acquisition.

### Bound tree

Continue using `BoundExpression.Array`, with its `ArrayTypeSymbol` carrying the capability.

No separate bound literal node is required unless lowering needs to distinguish storage strategy. Prefer capability on the type over parallel mutable and read-only node families.

### Lowering

Lower read-only literals through the existing array-creation path.

Mutable-to-read-only conversions should lower as representation-preserving copies or no-op conversions. They must not clone the array.

Lowering must never emit an element-write instruction whose statically known target type is read-only.

## IR changes

### Type transport

The existing IR type references can carry read-only capability through `ArrayTypeSymbol`.

`IrInstruction.CreateArray.Type` records whether the literal result is mutable or read-only.

No new array-read instruction is required.

### Validation

Update IR validation to:

- compare array capability during type equivalence;
- accept representation-preserving mutable-to-read-only conversions;
- accept checked read-only-to-mutable conversions while rejecting an unchecked capability acquisition;
- reject `SetElement` when the target slot is read-only;
- validate covariant read-only call arguments;
- validate read-only array constants and results using the correct runtime capability;
- preserve existing rules for reads and `length`.

Malformed third-party IR must not bypass the read-only write restriction.

## .NET exporter changes

### Runtime interfaces

Introduce `IDotNetReadOnlyArrayValue` with:

- stable identity;
- count;
- element reads.

Change `IDotNetArrayValue` to extend `IDotNetReadOnlyArrayValue` and retain element writes.

Existing mutable internal arrays implement `IDotNetArrayValue`. Providers may expose genuinely read-only arrays by implementing only `IDotNetReadOnlyArrayValue`.

This is an intentional alpha API change and requires public API and compatibility-baseline updates.

### Runtime validation

Runtime type validation must:

- accept either read-only or mutable implementations for `T[]$`;
- require `IDotNetArrayValue` for `T[]`;
- validate elements recursively against the declared element type;
- use the same capability-and-shape predicate for `is T[]` and `as T[]`;
- avoid requiring adapters to expose a nominal or reified element type;
- validate every element read against the statically expected element type;
- retain traversal-depth and cycle-safety behavior;
- preserve identity when a mutable value is observed as read-only.

### Provider-call boundary

Provider implementations declared with read-only array parameters must receive a value exposing only the read-only adapter contract.

Extend provider invocation metadata so the .NET exporter has access to parameter types when preparing arguments. The exporter may project a mutable adapter through an identity-preserving read-only wrapper before invoking the provider delegate.

The wrapper must:

- implement only `IDotNetReadOnlyArrayValue`;
- forward count and reads;
- forward the original logical identity;
- avoid copying elements;
- be reused within one invocation when the same array appears more than once.

This prevents provider implementations from acquiring mutation capability merely because the underlying value originated as a mutable MuLang array.

### Identity and equality

Structural equality remains based on visible elements.

Identity equality uses the forwarded logical identity, so a mutable array and every read-only view of it remain identical.

## Fingerprints and compatibility

Environment fingerprints must distinguish mutable and read-only array signatures.

Type canonicalization must include array capability before recursively encoding the element type.

Because the language version is already part of environment and profile identity, Version 1 and Version 2 environments remain incompatible even when their symbols otherwise match.

Public API baselines require updates in:

- `MuLang.Core`;
- `MuLang.Compiler` only if any new compiler-facing API is public;
- `MuLang.IR` through transported type APIs;
- `MuLang.Exporters.DotNet`.

## Documentation changes

Update the language specification sections covering:

- lexical structure;
- types;
- assignability and conversions;
- expressions and array literals;
- statements and assignment targets;
- operators, type tests, and checked conversions;
- provider environments;
- runtime adapters;
- language profiles;
- grammar summary.

Document explicitly that:

- `$` is a postfix read-only capability modifier;
- `$[` creates a read-only array literal;
- read-only is shallow;
- views preserve identity and observe mutations performed through other mutable aliases;
- covariance applies only to read-only views;
- mutable arrays remain invariant.

## Test plan

### Core tests

Cover:

- construction and display names;
- equivalence;
- assignability in both directions;
- view compatibility;
- nullable combinations;
- nested arrays;
- numeric representation-safe and representation-changing cases;
- common types;
- environment fingerprints.

### Compiler tests

Cover:

- tokenization of `$` and `$[`;
- Version 1 diagnostics;
- valid and invalid suffix placement;
- mutable and read-only literals;
- empty literal contextual typing;
- nested capability syntax;
- read and `length`;
- rejected writes;
- function arguments and returns;
- conditional and null-coalescing common types;
- `is` and `as` for mutable and read-only targets;
- successful and failed checked acquisition of mutable capability;
- preservation of mutable literal inference under `var`.

### IR tests

Cover:

- valid read-only creation and reads;
- valid mutable-to-read-only conversion;
- valid checked capability acquisition;
- rejected unchecked capability acquisition;
- rejected writes through malformed IR;
- provider-call covariance;
- nullable and nested array metadata.

### .NET exporter tests

Cover:

- mutable and genuinely read-only adapters;
- read-only views over mutable arrays;
- identity preservation;
- visibility of mutations through other aliases;
- provider-call argument projection;
- inability of provider implementations to cast projected values to the mutable interface;
- structural equality;
- runtime type tests and casts based on current element shape;
- failure of mutable casts for genuinely read-only adapters and protected projections;
- runtime read failure after another alias introduces a nonconforming element;
- invalid adapter and element values.

### End-to-end tests

Cover complete Version 2 compilation and execution for:

- read-only literal creation;
- covariant provider arguments;
- nested read-only arrays;
- read-only return values;
- checked casts from read-only views to mutable arrays;
- failed casts when write capability or element conformance is absent;
- mutation rejection at compile time;
- mutable aliases updating a read-only view.

## Implementation phases

### Phase 1: Type-system foundation

1. Add Version 2 language identities and profiles.
2. Extend `ArrayTypeSymbol` and `TypeSymbols`.
3. Implement equivalence, view compatibility, assignability, conversions, and common types.
4. Update environment fingerprints.
5. Add focused Core tests.

Exit criteria:

- all type relationships are defined independently of syntax;
- mutable arrays remain invariant;
- read-only covariance passes Core tests.

### Phase 2: Syntax and binding

1. Add `$` and `$[` tokens.
2. Extend type parsing and array-literal parsing.
3. Add version diagnostics.
4. Bind read-only types and literals.
5. Reject writes through read-only targets and require checked conversion for capability acquisition.
6. Add compiler tests.

Exit criteria:

- all agreed syntax compiles under Version 2;
- Version 1 rejects it cleanly;
- no read-only target can be assigned through source code.

### Phase 3: IR support

1. Carry capability through IR types.
2. Update conversion and write validation.
3. Update provider-call validation.
4. Add malformed-IR tests.

Exit criteria:

- validated IR preserves the same capability guarantees as source binding;
- third-party IR cannot encode a write through a read-only slot.

### Phase 4: .NET runtime contracts

1. Split read-only and mutable array adapter interfaces.
2. Update array operations and deep type validation.
3. Add provider-call read-only projection.
4. Preserve identity across projections.
5. Add exporter tests.

Exit criteria:

- genuinely read-only provider arrays execute correctly;
- provider calls cannot acquire write access from read-only parameters;
- no array copy is required for a view conversion.

### Phase 5: Documentation and API governance

1. Update the language specification.
2. Update package and API documentation.
3. Update public API baselines.
4. Record the alpha breaking changes.
5. Run Debug and Release validation.

Exit criteria:

- syntax, semantics, runtime behavior, and API changes are documented;
- all affected packages build, test, pack, and pass API validation.

## Final acceptance criteria

- `T[]$` is a supported Version 2 type.
- `$[...]` creates a read-only array literal.
- Mutable arrays remain invariant.
- Read-only views are safely covariant.
- Read-only capability cannot be removed implicitly.
- `as T[]` and `is T[]` use one shape-based runtime predicate requiring writable capability and currently conforming elements.
- Array casts do not require a nominal or reified runtime element type.
- Successful runtime conformance checks do not establish a permanent invariant across aliases.
- Element reads detect shape violations introduced later through another alias.
- Element assignment through a read-only type is rejected statically.
- Malformed IR cannot bypass the restriction.
- Providers can expose arrays without a write contract.
- Provider functions receive read-only projections for read-only parameters.
- View creation preserves storage and logical identity.
- Nullability, nesting, equality, and type tests have documented behavior.
- Version 1 behavior remains unchanged apart from improved unsupported-version diagnostics.
