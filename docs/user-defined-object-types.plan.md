# User-defined object types plan

## Status

Planned for MuLang `1.2`, targeting the package release `0.3.0`.

Introduce source-declared and inline object types alongside the complete
object-literal model in MuLang `1.2`.

The feature is controlled independently by a language-profile setting and is
enabled by the standard profile for the selected language version.

## Prerequisite

The read-only and optional object-property model is the prerequisite and is
considered complete. Resolve any remaining compatibility issues without
changing its settled semantics.

User-defined object declarations use that plan's complete property model:

- mutable or read-only capability;
- required or optional presence;
- explicit property types;
- recursive structural compatibility;
- portable-IR and runtime preservation of property capabilities.

## Goal

Allow a program to:

- declare named closed or open structured object types;
- use closed or open anonymous object types inline in every type position;
- use named and inline object types everywhere an existing host-declared
  structured type may be used.

The feature adds source-level structural type syntax and optional names without
introducing nominal identity. Compatibility, casts, type tests, runtime
conformance, and IR remain structural.

## Non-goals

This plan does not introduce:

- aliases for primitive, array, nullable, or arbitrary existing types;
- classes, interfaces, inheritance, methods, constructors, or generics;
- nominal object identity;
- nested or block-scoped type declarations;
- declarations in expression compilation mode;
- a named object-construction expression;
- automatic serialization of unused declarations;
- runtime reflection over source type names.

Object literals remain the only source value-construction syntax. Inline object
types describe static shapes and do not construct values.

## Type and declaration syntax

Add `type` as a keyword in the language version that introduces the feature.

The complete grammar is:

```ebnf
program-root
    = type-declaration* function-declaration* statement* end-of-file ;

type-declaration
    = "type" identifier object-type-body ";" ;

type
    = primary-type nullable-suffix?
      (array-suffix read-only-suffix? nullable-suffix?)* ;

primary-type
    = intrinsic-type
    | named-type
    | object-type-body ;

object-type-body
    = closed-object-type
    | open-object-type ;

closed-object-type
    = "{" object-type-property-list? "}" ;

open-object-type
    = "@{" object-type-property-list? "}" ;

object-type-property-list
    = object-type-property ("," object-type-property)* ","? ;

object-type-property
    = property-name "$"? "?"? ":" type ;

property-name
    = identifier
    | string-literal ;
```

`intrinsic-type`, `named-type`, and the suffix productions retain their
existing definitions.

An object body is therefore both:

- the complete shape attached to a top-level named declaration;
- a primary type that may occur inline in variable annotations, function
  parameters and returns, array element types, object properties, casts, type
  tests, and every other existing type position.

The ordinary suffix rules apply to an inline object body as a unit.
Consequently an inline closed or open object type may be nullable, may be an
array element type, and may be followed by any valid sequence of array,
read-only-array, and nullable suffixes without additional parentheses.

Object type bodies nest recursively because every property ends in `type`.
There is no separate grammar for a nested object shape.

The modifier order is fixed:

1. `$` for a read-only property;
2. `?` for an optional property;
3. `:` and the property type.

Every declared property requires an explicit type and cannot contain an
initializer. Property values belong to object literals, not type declarations.

The existing trailing-comma profile setting governs a trailing comma after the
last property. Empty closed and open declarations are valid.

Every open object type uses `@{ ... }`, whether named or inline, and is subject
to the existing open-objects profile setting. A disabled open-objects setting
produces the same category of feature diagnostic used for other unavailable
open-object constructs.

## Declaration placement

Type declarations are top-level program declarations.

All type declarations must precede all function declarations, and all function
declarations must precede executable statements. Add targeted syntax
diagnostics for:

- a type declaration after a function declaration;
- a type declaration after an executable statement;
- a function declaration after an executable statement, preserving the
  existing rule.

Type declarations are not permitted in blocks or other embedded-statement
positions.

`CompilationMode.Expression` rejects a leading type declaration with a targeted
diagnostic stating that type declarations are only valid at the start of a
program. It does not add a declaration prefix to expression roots.

## Names and scope

Source-declared and host-declared structured types share one program-wide type
namespace.

The type namespace remains separate from variable and function namespaces. A
type may therefore share its name with a variable or function.

The following are errors:

- two source declarations with the same name;
- a source declaration whose name matches a host-declared type;
- a declaration using an invalid language identifier.

For error recovery:

- the first source declaration wins over later duplicate source declarations;
- a host type wins over a conflicting source declaration;
- the conflicting declaration is still traversed for independent syntax and
  property diagnostics but is not available for type resolution.

Distinct source names may declare structurally equivalent shapes. The names do
not make those shapes distinct.

All valid source type names are visible throughout the program, including in
earlier declarations and in every function signature. Declaration order does
not affect resolution.

## Structural semantics

A source-declared type is an `ObjectTypeSymbol` with:

- its source name as `Name`;
- no provider identifier;
- the declared open or closed shape;
- the property declarations defined by the source body.

Source type names are descriptive compile-time names, not identity-bearing
provider names.

An inline object type is an `ObjectTypeSymbol` with:

- no provider identifier;
- the anonymous display name;
- the inline open or closed shape;
- the property declarations defined by that occurrence.

Each inline occurrence creates a distinct compiler symbol. Symbol identity is
not language identity: structurally equivalent inline, source-declared,
host-declared, and inferred anonymous object types remain equivalent under the
ordinary structural rules.

Every existing structural rule applies unchanged after the property-capability
prerequisite:

- equivalence ignores source names;
- assignability compares complete structural shape and capabilities;
- casts and type tests validate runtime shape and capabilities;
- open and closed types retain their existing behavior;
- optionality remains distinct from nullability;
- mutable members retain invariant requirements;
- read-only members use the compatibility rules established by the
  prerequisite plan.

A source type can be structurally compatible with:

- another source-declared type;
- a host-declared type;
- an anonymous type written inline;
- an anonymous type inferred from an object literal.

No explicit conversion is required merely because the names differ.

## Recursive declarations

Direct recursion, mutual recursion, and forward references are permitted.

Type recursion is independent from `RecursionFeature`, which continues to
control user-function recursion only. No additional type-recursion profile
setting is introduced.

Binding must operate in two declaration passes:

1. validate names and create incomplete `ObjectTypeSymbol` instances for every
   accepted source declaration;
2. bind every property type against the complete source and host type
   namespaces, then complete each object symbol.

Nullable and array constructions may contain incomplete source object symbols
while the graph is being assembled.

Inline object types do not introduce a name and therefore cannot refer directly
to themselves. They may refer to any visible named source or host type,
including the named declaration whose body contains the inline occurrence.
They can consequently participate in a recursive graph through named types
without requiring anonymous-type references or synthetic names.

Recursive graphs use the existing coinductive structural algorithms and must
terminate in:

- equivalence;
- assignability and view compatibility;
- checked-cast feasibility;
- runtime conformance;
- IR projection and canonicalization;
- MuIR serialization.

A recursive cycle containing only required properties is valid even when no
finite acyclic object literal can construct it. Such a declaration may still
describe a cyclic runtime graph or a structurally compatible host value and
does not produce a warning.

## Object construction and contextual typing

No constructor or named creation expression is added.

An object literal may acquire a source-declared type through every existing
expected-type position, including:

- a variable annotation;
- a function parameter;
- a function return type;
- an array element context;
- a containing object property;
- a checked cast or other existing type-bearing construct where applicable.

The expected type may be named or inline. Contextual object-literal binding
treats source-declared, inline anonymous, and host-declared structured types
identically. The prerequisite plan's rules determine:

- required-property presence;
- omitted optional properties;
- explicit literal property types;
- mutable and read-only capability matching;
- additional properties for open types;
- initializer conversions.

An uncontextualized object literal continues to infer an anonymous structural
type. The compiler does not infer a declared source name merely because its
shape is equivalent, and it does not retain a relationship to an equivalent
inline type occurrence beyond ordinary structural equivalence.

## Language version and profile

Add a public `UserDefinedTypesFeature` enum:

- `Disabled = 0`;
- `Enabled = 1`.

Add:

- `LanguageProfile.UserDefinedTypes`;
- a constructor parameter in the internal profile constructor;
- `LanguageProfileBuilder.WithUserDefinedTypes`;
- builder copying and validation;
- language-profile fingerprint participation;
- test-profile factory support.

The setting is effective only in the language version selected for this
feature. In earlier versions it is dormant and `type` retains its previous
identifier behavior. Object bodies are not recognized as primary types before
the introducing version.

For the introducing language version:

- the standard profile enables the feature;
- custom profiles may disable it;
- the lexer recognizes `type` as `TypeKeyword`;
- the parser still constructs named declarations and inline object type syntax
  when the feature is disabled;
- binding reports a targeted feature diagnostic for each named declaration and
  inline object type occurrence.

The same `UserDefinedTypesFeature` setting governs named declarations and inline
object type syntax. Disabling the feature must not turn either construct into
cascading syntax errors.

If the feature is assigned to `1.3`, add `LanguageVersion.Version1_3`,
`LanguageProfiles.Version1_3`, and make it `Latest`. Preserve the existing
numeric values of all earlier versions.

## Syntax model

Add syntax nodes in separate files:

- `TypeDeclarationSyntax`;
- `TypePrimarySyntax`;
- `NamedTypeSyntax`;
- `ObjectTypeBodySyntax`;
- `ObjectTypePropertySyntax`.

`ProgramRootSyntax` records type declarations before functions and statements.

`TypeDeclarationSyntax` records:

- the `type` keyword;
- the declared-name token;
- the object body;
- the terminating semicolon.

`ObjectTypeBodySyntax` records:

- the closed or open brace token;
- the property declarations;
- comma tokens;
- the closing brace token;
- whether the declaration is open.

`ObjectTypePropertySyntax` records:

- the property-name token;
- an optional `$` token;
- an optional `?` token;
- the `:` token;
- the property `TypeSyntax`.

Refactor the current flat `TypeSyntax` shape. It should record:

- a `TypePrimarySyntax`;
- the existing nullable, array, and read-only suffix tokens in source order.

`NamedTypeSyntax` wraps an intrinsic keyword, `void` where permitted, or an
identifier type name. `ObjectTypeBodySyntax` is also a `TypePrimarySyntax`.
This makes nesting explicit in the syntax tree while keeping suffix parsing and
validation shared across named, intrinsic, and object primary types.

Reuse the prerequisite plan's property-name decoding and modifier validation
where possible. Named declarations and inline types use the same
`ObjectTypeBodySyntax` parser and binder. Do not duplicate object-literal,
named-declaration, and inline-type logic for:

- identifier and string-literal property names;
- duplicate-name detection;
- modifier order and repetition;
- type-syntax parsing;
- trailing-comma validation.

The declaration parser must recover at commas, the closing brace, the
terminating semicolon, and the next top-level declaration or statement.
Inline object type parsing must additionally recover at the ordinary boundary
of its containing type position, including a property initializer, parameter
separator, closing parenthesis, assignment token, statement terminator, or
type-operator continuation.

## Binding model

Extend the program binder with a source-type table separate from the existing
user-function table.

Program binding order becomes:

1. declare source type names and incomplete symbols;
2. bind and complete source type bodies;
3. declare user-function signatures;
4. bind user-function bodies;
5. validate user-function recursion when required;
6. bind executable statements.

This ordering makes source types available in function signatures and makes
all function signatures available while function bodies are bound.

`BindType` dispatches on the primary type.

For a `NamedTypeSyntax`, it resolves identifier types in this order:

1. the source-type table;
2. the host environment;
3. undefined-type recovery.

Name-conflict validation ensures that the first two sources cannot both provide
a valid symbol for the same name.

Add an internal `ObjectTypeSymbol.CreateSourceIncomplete` factory that:

- validates the source language name;
- stores no provider identifier;
- creates an incomplete source-named symbol;
- can be completed exactly once through the existing completion mechanism.

Keep `CreateAnonymous` for inferred object literals and use it for inline
object type bodies after their properties have been bound. Update XML
documentation that currently equates a missing provider identifier exclusively
with anonymity.

Extract shared object-type-body binding used by named declarations and inline
primary types. When binding a type body:

- bind every property `TypeSyntax`;
- reject `void`, `null`, and error-only recovery types as property types using
  the existing type-validity conventions;
- decode property names consistently with object literals;
- report duplicate properties without passing duplicates to
  `ObjectTypeSymbol.Complete`;
- carry optional and read-only capability into `ObjectPropertySymbol`;
- complete a named incomplete symbol even after recoverable property errors so
  later declarations can continue binding;
- create and return an anonymous symbol for an inline occurrence.

After binding the primary type, apply nullable and array suffixes through the
existing type-construction rules. Suffix behavior must not depend on whether
the primary type was named, intrinsic, or an inline object body.

Avoid separately caching or interning structurally equivalent inline
occurrences. Existing type relations and IR canonicalization provide structural
behavior without making frontend symbol construction depend on global shape
interning.

No source declaration is added to `EnvironmentSchema`. The environment remains
the provider contract and its fingerprint remains independent from program
declarations.

## Diagnostics

Add stable diagnostics for:

- type declarations disabled by the language profile;
- inline object types disabled by the language profile;
- type declarations in expression mode;
- type declarations after functions;
- type declarations after executable statements;
- duplicate source type declarations;
- conflicts with host-declared types;
- duplicate declared properties;
- invalid declared property types;
- malformed property modifiers;
- malformed nested inline object types;
- missing body, separator, type, closing brace, or semicolon.

Reuse existing diagnostics where their meaning is already exact:

- undefined type;
- repeated nullable annotation;
- trailing separator;
- open-object feature disabled;
- duplicate object property, if its wording is generalized to cover both
  literals and declarations without losing precision.

Feature diagnostics remain separately suppressible through the existing
compiler feature-diagnostic controls.

## Semantic classification and editor support

Classify:

- `type` as a keyword;
- the declared type name as `SemanticClassificationKind.Type` with the
  declaration modifier;
- every identifier type reference with the existing type classification;
- property names in named and inline bodies with the existing property
  classification;
- nested property type syntax recursively through the type visitor.

Update the TextMate grammar for the new keyword and declaration context.

The language server requires no protocol extension. Existing semantic-token
transport already supports named-type declarations and references; add coverage
for source declarations, inline object bodies, nested object types, forward
references, and recursive property types.

## Core type model

Do not introduce a new public `TypeKind`.

Source declarations, inline object types, host declarations, and anonymous
inferred shapes all use `ObjectTypeSymbol`. Their semantic distinction is
limited to:

- provider identifier presence;
- source or host display name;
- anonymous display name;
- frontend declaration metadata.

Do not add nominal identity to `TypeRelations`, object graph signatures,
environment fingerprints, or IR canonicalization.

The compiler's source-declaration syntax association should remain in compiler
symbols or binder tables rather than in the public Core type model.

## Portable IR and MuIR

No new instruction, type kind, or declaration table is required.

Lowering continues to project every reachable `ObjectTypeSymbol` to an
anonymous structural graph through `IrTypeProjector`. This deliberately erases:

- source type names;
- source declaration order;
- source-versus-host origin;
- provider identifiers.

Only shapes reachable from function signatures, slots, constants, casts, type
tests, object creation, arrays, and nested properties appear in portable IR.
Unused source declarations are not emitted. An inline type is necessarily
reachable through the containing type-bearing construct if that construct is
lowered.

The property-capability prerequisite is responsible for any MuIR format change
needed for read-only or optional properties. User-defined type names alone do
not require another MuIR version.

Add regression tests proving that:

- equivalent source names lower to equivalent structural IR;
- equivalent inline occurrences lower to equivalent structural IR;
- nested inline shapes lower recursively and deterministically;
- recursive source declarations project without infinite recursion;
- unused declarations do not affect serialized MuIR;
- renaming a source type without changing a reachable shape does not alter
  canonical MuIR;
- changing a reachable shape does alter the projected type table.

## .NET exporter and runtime

No exporter or runtime representation is added specifically for source type
names.

The exporter receives only projected structural IR and therefore treats values
of source-declared and inline object types exactly like equivalent
host-declared or inferred anonymous structured values.

Runtime construction, mutation, removal, conformance, and cyclic-graph behavior
are covered by the object-property prerequisite. Add integration tests using
source declarations to verify that those existing paths are exercised after
name erasure.

## Public API impact

Add the following public profile API:

- `UserDefinedTypesFeature`;
- `LanguageProfile.UserDefinedTypes`;
- `LanguageProfileBuilder.WithUserDefinedTypes`;
- the standard language profile for the selected version;
- `LanguageVersion.Version1_3` only if the feature targets `1.3`.

Do not expose:

- a public collection of source declarations;
- source names in `IrProgram`;
- source declaration identities in `ObjectTypeSymbol`;
- a host API for registering source types.

Update public API baselines after implementation.

## Documentation updates

Update:

- overview and unsupported-feature lists;
- lexical structure and reserved keywords;
- types;
- assignability and conversions;
- expressions and object literals;
- user-defined functions, where declaration ordering is described;
- host environment and runtime values;
- language profiles;
- grammar summary;
- portable IR and MuIR name-erasure notes;
- language-server semantic highlighting;
- roadmap status;
- detailed changelog.

Document explicitly that:

- names are compile-time aliases for structural object shapes, not nominal
  identities;
- object bodies are first-class primary type syntax and may be nested inline;
- inline object types may use every ordinary nullable and array suffix;
- each inline occurrence is anonymous but participates in structural
  equivalence;
- declarations are program-wide and support forward and mutual references;
- source and host types can be structurally compatible;
- object literals remain the construction mechanism;
- recursive required-property cycles are valid;
- unused declarations do not appear in portable IR;
- source names are erased during lowering.

## Test plan

Add Core tests covering:

- `UserDefinedTypesFeature` numeric values and validation;
- builder copying and `WithUserDefinedTypes`;
- profile fingerprints;
- standard-profile defaults;
- dormant behavior before the introducing version;
- source-named incomplete object creation and completion;
- structural equivalence independent from source names;
- direct and mutual recursive source-shaped graphs.

Add lexer and parser tests covering:

- version-aware `type` keyword recognition;
- closed, open, empty, and recursive declarations;
- closed, open, empty, and nested inline object types in every type position;
- nullable, mutable-array, and read-only-array suffixes on inline object types;
- identifier and string-literal property names;
- `$?` modifier order;
- nested nullable and array property types;
- trailing-comma profile behavior;
- missing punctuation and recovery;
- declaration ordering;
- expression-mode rejection;
- disabled-feature parsing for declarations and inline types without diagnostic
  cascades;
- recovery from malformed nested inline bodies.

Add binder tests covering:

- source type resolution in variables, function signatures, returns, arrays,
  object properties, casts, and type tests;
- inline object types in variables, function signatures, returns, arrays,
  object properties, casts, and type tests;
- structural compatibility among inline, source-declared, host-declared, and
  inferred anonymous types;
- nested inline object binding;
- inline object types referring to named source types, including the containing
  named declaration;
- nullable and array constructions over inline object types;
- forward and mutual references;
- duplicate source names;
- host-type conflicts;
- separate type and value/function namespaces;
- equivalent declarations with distinct names;
- contextual object literals;
- open-object profile interaction;
- duplicate and invalid properties;
- recursive required-property cycles;
- diagnostics from invalid declarations without loss of later binding.

Add compiler editor tests covering:

- declared-type semantic classification;
- forward and recursive type-reference classification;
- inline and nested property-name and property-type classification.

Add IR and serialization tests covering:

- reachable source shapes;
- reachable inline and nested-inline shapes;
- unused declarations;
- source-name erasure;
- deterministic recursive projection;
- structural equivalence with host and anonymous types.

Add exporter integration tests covering:

- values created from contextually typed object literals;
- source and host structural interchange;
- checked casts and type tests;
- recursive and cyclic values;
- property capability enforcement from the prerequisite plan.

Update language-server tests for standard and Visual Studio presentation modes.

## Implementation sequence

1. Keep the completed read-only and optional object-properties model as a
   prerequisite; resolve any remaining compatibility issues without changing
   its settled semantics.
2. Add `UserDefinedTypesFeature` to profiles, builders, fingerprints, standard
   profiles, test factories, public APIs, and baselines.
3. Refactor `TypeSyntax` around recursive primary types and shared suffixes.
4. Add version-aware `type` tokenization, declaration syntax nodes, and shared
   object-type-body syntax.
5. Extend type parsing with closed and open inline object primary types.
6. Extend program parsing, declaration ordering, expression-mode recovery, and
   targeted feature diagnostics.
7. Add source-named incomplete object-symbol construction in Core.
8. Implement shared named and inline object-type-body binding.
9. Implement the two-pass source-type declaration and completion pipeline.
10. Resolve source and inline types from all existing type positions and bind
    function signatures after named type completion.
11. Integrate contextual object-literal binding and open-object validation.
12. Add recursive semantic classification and TextMate support.
13. Verify reachable-only structural IR projection and source-name erasure.
14. Add compiler, Core, IR, serialization, exporter, and language-server
    coverage.
15. Update specifications, roadmap, changelog, and public API baselines.

## Decisions

The following decisions are fixed for this plan:

- the feature targets MuLang `1.2` and package release `0.3.0`;
- syntax uses `type Name { ... };` and `type Name @{ ... };`;
- closed and open object bodies are also valid inline primary types;
- inline object types are governed by `UserDefinedTypesFeature`;
- inline object types support ordinary nullable and array suffixes;
- nested object shapes use recursive inline `type` syntax;
- inline occurrences have no name and cannot refer directly to themselves;
- compatibility is purely structural;
- the complete object-property grammar is a strict prerequisite;
- forward references and direct or mutual recursion are supported;
- source and host type-name conflicts are errors;
- object literals are the only construction syntax;
- arbitrary type aliases are out of scope;
- declarations precede functions, which precede statements;
- type recursion is independent from function recursion;
- property names may be identifiers or string literals;
- multiple names may describe equivalent shapes;
- disabled features produce targeted diagnostics after successful parsing;
- type names use a separate namespace from variables and functions;
- source names are erased during lowering;
- unused declarations are not emitted to IR;
- declarations are program-only;
- recursive required-property cycles are valid;
- the feature has its own profile setting;
- the exact language version remains open between `1.2` and `1.3` under the
  stated release criterion.
