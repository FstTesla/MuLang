# Read-only and optional object properties plan

## Status

Proposed for MuLang `1.2`.

## Goal

Extend structured object properties and object literals with:

- read-only properties whose value and presence are fixed at construction;
- optional properties that may be absent from a newly created object;
- explicit property-type annotations in object literals;
- a non-conflicting `=` initializer syntax;
- a language-profile setting that preserves the legacy literal grammar when
  required.

The feature must apply consistently to inferred anonymous object types,
host-declared structured types, structural compatibility, mutation and removal,
portable IR, MuIR, and .NET execution.

## Full object-literal syntax

Introduce this property grammar:

```ebnf
object-property =
    property-name read-only-modifier? optional-modifier?
    property-type? property-initializer? ;

property-name =
    identifier
    | string-literal ;

read-only-modifier = "$" ;
optional-modifier = "?" ;
property-type = ":" type ;
property-initializer = "=" expression ;
```

The modifier order is fixed:

1. `$`;
2. `?`;
3. `: Type`;
4. `= expression`.

Reject repeated modifiers, `?` before `$`, modifiers after the type, and
modifiers after the initializer.

The property forms have these requirements:

| Property form | Explicit type | Initializer |
|---|---:|---:|
| Required mutable | Optional | Required |
| Required read-only | Optional | Required |
| Optional mutable, present | Optional | Required |
| Optional read-only, present | Optional | Required |
| Optional mutable, absent | Required | Omitted |
| Optional read-only, absent | Required | Omitted |

A required property without an initializer is invalid.

An optional property without an initializer is absent from the created value.
It is not present with a `null` value.

An omitted initializer requires an explicit type because no value is available
for inference.

When both an explicit type and an initializer are present, the initializer must
be implicitly assignable to the declared type. The declared type becomes the
property type even when the initializer has a more specific type.

Without an explicit type, infer the property type from the initializer using
the existing object-property inference rules. `null`, `void`, and error-recovery
values remain insufficient for inference.

## Legacy object-literal syntax

Preserve the current grammar under the legacy profile setting:

```ebnf
legacy-object-property =
    property-name (":" | "?" ":") expression ;
```

Legacy literals:

- always require an initializer;
- do not declare read-only properties;
- do not permit explicit property types;
- retain the current contextual-typing behavior;
- continue interpreting `?` followed by `:` as the optional-property marker.

The lexer should stop treating `?:` as one indivisible
`OptionalPropertyColon` token. Tokenize it as `Question` followed by `Colon`,
then let the selected object-literal parser interpret the sequence. This keeps
the legacy source spelling valid while allowing the full grammar to parse an
optional modifier immediately followed by a type annotation.

## Language-profile setting

Add a public `ObjectLiteralSyntax` enum:

- `Legacy = 0`.
- `Full = 1`;

Add `LanguageProfile.ObjectLiteralSyntax` and
`LanguageProfileBuilder.WithObjectLiteralSyntax`.

The standard profiles use:

- MuLang 1: `Legacy`;
- MuLang 1.1: `Legacy`;
- MuLang 1.2: `Full`.

The setting is effective only for MuLang 1.2 or later. For earlier language
versions it is dormant and parsing always uses the legacy grammar. This follows
the existing profile rule that a setting whose prerequisite is unavailable
does not invalidate the profile.

The configured value still participates in the language-profile fingerprint,
including when dormant, consistently with the other profile settings.

Under MuLang 1.2:

- `Full` accepts only the full grammar;
- `Legacy` accepts only the legacy grammar.

Using syntax from the other mode should produce a targeted syntax diagnostic
rather than cascading generic expression errors.

## Property model

Extend `ObjectPropertySymbol` with `IsReadOnly`.

Its public constructor should accept:

- name;
- type;
- optionality;
- read-only capability.

Preserve source compatibility where practical by adding the new argument after
the existing optionality argument with a default of `false`.

A structured property consequently has four independent attributes:

- name;
- value type;
- required or optional presence;
- mutable or read-only capability.

Nullability remains part of the value type and is independent from
optionality.

Read-only means that the property's presence and value are frozen when object
construction completes:

- a present read-only property cannot be assigned or removed;
- an absent read-only optional property cannot be added later;
- read-only does not imply that the referenced value is deeply immutable;
- contained objects and arrays retain the capabilities expressed by their own
  types.

Mutable optional properties retain variable presence:

- an absent property may be added by assignment;
- a present property may be assigned;
- a present property may be removed;
- an absent property still fails ordinary access and produces `null` through
  optional access.

Required properties must be present after construction and cannot be removed,
regardless of mutability.

## Contextual typing

An object literal may still be contextually typed by a structured object type.

For every explicitly listed property:

- the name must exist unless the expected type is open;
- an explicit source type must be equivalent to the expected known property
  type;
- an initializer is bound using the explicit type when present, otherwise the
  expected property type when available;
- source optionality and read-only modifiers must be validated against the
  expected property declaration;
- an omitted property initializer is valid only for an optional expected
  property.

For properties omitted entirely from the literal:

- every expected required property produces the existing missing-property
  diagnostic;
- expected optional properties remain absent.

The expected type remains the resulting static type when contextual typing
succeeds. The literal syntax must not silently weaken or strengthen known
property capabilities.

Legacy contextual typing retains the existing behavior: the expected structured
type supplies optionality and read-only capability because the legacy grammar
cannot express the latter.

## Type relations

Update structural equivalence so `IsReadOnly` contributes to property identity.

Retain invariant compatibility for mutable target properties.

For a read-only target property, permit a representation-safe read view:

- the source property may be mutable or read-only;
- source and target optionality must remain compatible;
- the source value type must be view-compatible with the target value type;
- assigning a read-only source property to a mutable target property is not
  permitted.

This mirrors read-only array views without introducing a separate object-view
type.

Checked casts and type tests must include property capability:

- a mutable target property requires the runtime value to support the existing
  writable behavior expected by that cast;
- a read-only target only requires readable conformance;
- required and optional presence rules remain part of conformance;
- an absent read-only optional property conforms and remains absent;
- recursive object graphs must continue terminating coinductively.

Environment and language-profile fingerprints must change when property
read-only capability or the object-literal syntax setting changes.

## Syntax model

Replace the current syntax node shape based on one separator and one mandatory
value.

`ObjectPropertyInitializerSyntax` should record:

- the property-name token;
- an optional `$` token;
- an optional `?` token;
- an optional `:` token and `TypeSyntax`;
- an optional `=` token and value expression;
- whether it was parsed using full or legacy syntax when that distinction is
  useful for diagnostics.

Its span must end at the initializer when present, otherwise at the explicit
type.

Keep closed and open object literals on the same property grammar.

Update source classification so:

- `$`, `?`, `:`, and `=` retain their ordinary operator or punctuation
  classifications;
- explicit property types receive the same lexical and semantic
  classifications as other type syntax;
- property names remain property classifications.

Update the TextMate grammar only where removal of the combined `?:` token
changes lexical matching.

## Binding

Refactor object-literal binding into syntax-independent property-declaration
binding.

For each property:

1. resolve its name;
2. resolve the contextual property when available;
3. bind an explicit type when present;
4. select the initializer's expected type;
5. bind the initializer when present;
6. infer or validate the property type;
7. validate optionality and read-only capability;
8. add the declaration to the anonymous object type;
9. add a runtime property value only when an initializer is present.

The bound representation must distinguish declarations from present values.
Extend `BoundExpression.Object` to carry:

- the complete `ObjectTypeSymbol`;
- only the properties present in the created value.

An omitted optional initializer must not lower a synthetic `null`, default
value, or placeholder.

Add targeted diagnostics for:

- missing initializer on a required property;
- missing type on an uninitialized optional property;
- initializer not assignable to the declared type;
- modifier order, repetition, or placement;
- read-only or optional capability conflicting with an expected property;
- full syntax used in legacy mode;
- legacy syntax used in full mode;
- attempts to assign or remove a known read-only property.

## Assignment and removal

Update member and element assignment binding:

- reject assignment to every statically known read-only property;
- reject adding an absent statically known read-only optional property;
- continue permitting assignment to mutable required and optional properties;
- preserve profile-level mutation diagnostics independently from property
  capability diagnostics.

Update property-removal binding:

- reject every statically known read-only property;
- continue rejecting required properties;
- permit removal only for present mutable optional properties and existing
  dynamic-property cases.

Dynamic access through `object` or an open additional-property space cannot
prove a known property capability statically. Runtime-created literal objects
must therefore preserve and enforce their property declarations so that a
read-only property cannot be mutated through a less specific alias.

## Core object graph and fingerprints

Extend `ObjectTypeGraphBuilder.AddProperty` with read-only capability and carry
it through incomplete and recursive object graph construction.

Update:

- structural equivalence keys;
- environment fingerprint canonicalization;
- IR type canonicalization;
- deterministic property ordering;
- public API baselines and XML documentation.

Property read-only capability must be identity-bearing in every canonical type
representation.

## Portable IR

Object types in portable IR must retain property optionality and read-only
capability.

`IrInstruction.CreateObject` already separates the complete static object type
from the collection of present values. Formalize and validate that distinction:

- every supplied value names a declared property or a valid open-object
  additional property;
- supplied names are unique;
- every supplied value has the declared property type;
- every required known property is supplied;
- an optional known property may be omitted;
- omitted properties are not synthesized;
- object creation is the only operation allowed to establish a read-only
  property's initial presence and value.

Update `SetProperty`, `SetElement` for object access, `RemoveProperty`, and
`RemoveElementProperty` validation to reject statically known read-only
properties.

No new runtime instruction is required if `CreateObject` continues carrying
the full object type and present property values separately.

## MuIR

Serialize property read-only capability as part of every structured-object type
entry, alongside required or optional presence.

The wire property form should contain independent presence and mutability
tokens:

```text
property ::= string type-ref presence mutability
presence ::= "required" | "optional"
mutability ::= "mutable" | "readonly"
```

The final token order must be fixed and canonical.

This changes the MuIR type grammar and type identity.

Before implementation, determine the release state of MuIR version 2:

- if no published release has emitted version 2, extend version 2 directly and
  update its fixtures;
- if version 2 has been published, introduce the next MuIR format version,
  keep reading versions 1 and 2, and emit the new version.

Do not silently reinterpret an already published version.

Update deterministic type-table canonicalization so read-only capability
contributes to recursive structural signatures.

## .NET exporter and runtime

Extend the internal literal-object representation to retain:

- the complete known property declarations;
- the currently present values;
- the set of read-only known properties.

Object construction must populate initial values without going through ordinary
post-construction mutation checks, then freeze the declared read-only
capabilities before exposing the value.

The built-in object value must reject:

- assignment to a present read-only property;
- addition of an absent read-only optional property;
- removal of a present read-only optional property;
- removal of required properties.

The rejection must use the existing mutation and removal runtime-error
categories and stable error paths.

Host-provided object adapters remain trusted boundary implementations.
Document that a host value declared with read-only properties must preserve
those restrictions across aliases. Existing adapter methods may still reject
operations more strictly than the static type.

If runtime checked casts need to distinguish per-property write capability,
prefer adding a separate optional capability interface over adding breaking
members to `IDotNetObjectValue`. The built-in literal-object adapter should
implement that interface.

Provider boundary validation must recursively validate:

- required-property presence;
- present optional-property values;
- value types;
- read-only capability when the adapter exposes capability metadata.

## Standard library

Standard-library structured types may declare read-only and optional properties
through the extended `ObjectPropertySymbol` API.

No standard-library symbol is required solely for this feature.

Selection, composition, declaration parity, and .NET binding tests must include
the new property capability in equality and collision-sensitive schemas.

## Test plan

Add Core tests covering:

- `ObjectLiteralSyntax` validation, builder copying, and fingerprints;
- standard profile defaults by language version;
- dormant full syntax before MuLang 1.2;
- `ObjectPropertySymbol.IsReadOnly`;
- structural equivalence and incompatibility matrices;
- read-only view compatibility;
- optionality and nullability independence;
- recursive object graphs;
- environment fingerprint changes.

Add lexer and parser tests covering:

- split `?` and `:` tokens;
- every valid full property form;
- legacy `:` and `?:` forms;
- fixed `$?` modifier order;
- repeated and misplaced modifiers;
- explicit nested, nullable, array, and structured property types;
- omitted optional initializers;
- targeted cross-mode diagnostics;
- closed and open literals;
- trailing commas after initialized and uninitialized properties.

Add binder tests covering:

- inferred and explicit property types;
- contextual typing;
- initializer conversions;
- absent optional properties;
- required-property diagnostics;
- mutable and read-only assignment;
- mutable optional addition and removal;
- read-only optional frozen presence;
- property access and optional access;
- duplicate names;
- `null` with and without an explicit type;
- semantic classification of explicit types and properties.

Add IR tests covering:

- creation with omitted optional properties;
- rejection of omitted required properties;
- property-value type validation;
- read-only assignment and removal rejection;
- dynamic access behavior;
- recursive object types carrying read-only capability.

Add MuIR tests covering:

- canonical read-only and optional property tokens;
- deterministic recursive-type serialization;
- old-version compatibility;
- rejection of capability tokens in older versions;
- malformed capability combinations;
- canonical fixture updates.

Add exporter tests covering:

- present and absent mutable optional properties;
- present and absent read-only optional properties;
- mutation through direct, `object`, and checked-cast aliases;
- removal behavior;
- provider globals, arguments, and return values;
- cyclic object graphs;
- runtime errors for rejected mutations.

Update public API baselines after implementation is complete.

## Documentation updates

Update:

- lexical structure;
- types and structural compatibility;
- expressions and object literals;
- assignment and property removal;
- language profiles;
- grammar summary;
- host environment and runtime values;
- portable IR;
- MuIR;
- package and .NET adapter documentation;
- editor support;
- detailed changelog;
- roadmap status.

Document explicitly:

- optionality is presence, not nullability;
- read-only freezes value and presence after construction;
- omission creates no runtime property;
- `$?` is the canonical combined modifier order;
- `Legacy` is a migration mode, not the standard MuLang 1.2 grammar.

## Implementation sequence

1. Finalize contextual capability compatibility and the MuIR version decision.
2. Add `ObjectLiteralSyntax` to Core profiles, builders, fingerprints, standard
   profiles, public APIs, and tests.
3. Extend `ObjectPropertySymbol`, object graphs, type relations, environment
   fingerprints, and recursive canonicalization.
4. Split lexical `?:` handling and add full and legacy parser paths with
   targeted diagnostics.
5. Extend syntax nodes, semantic classification, and editor grammar support.
6. Refactor object-literal binding for explicit types, omitted values,
   optionality, and read-only capability.
7. Enforce read-only assignment and removal statically.
8. Strengthen portable IR object-creation and mutation validation.
9. Add MuIR property-capability serialization and compatibility handling.
10. Extend the .NET literal-object representation and runtime enforcement.
11. Add provider-boundary, aliasing, recursive-shape, and standard-library
    coverage.
12. Update specifications, API baselines, roadmap, and detailed changelog.

## Decisions required before implementation

- Confirm whether a required source property may initialize an optional
  contextual property, or whether optionality must match exactly.
- Confirm whether read-only target properties use covariant read compatibility
  or require equivalent value types.
- Confirm whether an explicitly declared source type must exactly match a
  contextual property type or may use an implicit widening conversion.
- Determine whether MuIR version 2 has already been published before changing
  its object-property grammar.
