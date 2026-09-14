# Language Feature Configuration Implementation Plan

## Status

This document is a temporary implementation plan for language feature configuration. It does not yet modify the normative language specification.

The implementation must preserve the complete current language behavior when the default profile is used.

## 1. Goals

The feature-configuration system should:

- let a provider restrict selected language constructs without maintaining a grammar fork;
- distinguish language-version evolution from provider-selected restrictions;
- report dedicated diagnostics for recognized but disabled features;
- keep parsing and binding deterministic for a given profile;
- contribute to compilation cache identity;
- represent each configurable concern with its own enum type;
- use `[Flags]` only when one concern contains independently combinable subfeatures;
- remain extensible for comments, user-defined types, and first-class functions.

## 2. Non-goals

The first implementation should not:

- add comments, user-defined types, first-class functions, or truthiness;
- introduce multiple language versions;
- make operator precedence or core value semantics configurable;
- change the environment fingerprint;
- provide per-function, per-type, or per-source-region feature overrides;
- provide allowlists for individual provider functions;
- expose feature configuration to the runtime after compilation.

## 3. Configuration model

Introduce a public immutable `LanguageProfile`.

The profile should contain:

- `LanguageVersion`;
- one enum-valued property for each configurable concern;
- a deterministic profile fingerprint.

`CompilationMode` should remain selected by the compilation API rather than being stored in the reusable profile. Expression and program compilation may use the same profile.

The profile should be accepted by:

- `MuLangCompiler.CompileExpression`;
- `MuLangCompiler.CompileProgram`.

Existing overloads should remain and use the standard profile by default.

The standard profile must enable every currently implemented feature and preserve all current policies.

## 4. Profile options

Each configurable concern has its own enum type and one corresponding `LanguageProfile` property.

Ordinary enums are used even when the initial option has only `Disabled` and `Enabled`. This permits future non-Boolean values without replacing the public property type.

Enums whose values represent independently combinable permissions use `[Flags]`. Their combination semantics are defined by the corresponding option section.

### 4.1. Initial option table

| Profile property | Enum type | Combinable | Values | Standard value |
|---|---|:---:|---|---|
| `UserDefinedFunctions` | `UserDefinedFunctionsFeature` | No | `Disabled = 0`, `Enabled = 1` | `Enabled` |
| `Recursion` | `RecursionFeature` | No | `Disabled = 0`, `Enabled = 1` | `Enabled` |
| `Loops` | `LoopFeatures` | Yes | `None = 0`, `While = 1`, `For = 2` | `While | For` |
| `ProviderFunctionCalls` | `ProviderFunctionCallsFeature` | No | `Disabled = 0`, `Enabled = 1` | `Enabled` |
| `OpenObjects` | `OpenObjectsFeature` | No | `Disabled = 0`, `Enabled = 1` | `Enabled` |
| `Mutations` | `MutationFeatures` | Yes | `None = 0`, `ObjectProperties = 1`, `ArrayElements = 2`, `PropertyRemoval = 4` | `ObjectProperties | ArrayElements | PropertyRemoval` |
| `OptionalAccess` | `OptionalAccessFeature` | No | `Disabled = 0`, `Enabled = 1` | `Enabled` |
| `MultiLevelLoopControl` | `MultiLevelLoopControlFeature` | No | `Disabled = 0`, `Enabled = 1` | `Enabled` |
| `TrailingCommas` | `TrailingCommasFeature` | No | `Disabled = 0`, `Enabled = 1` | `Enabled` |
| `ConditionSemantics` | `ConditionSemantics` | No | `StrictBoolean = 0`, `Truthiness = 1` | `StrictBoolean` |
| `Shadowing` | `ShadowingPolicy` | Yes | `None = 0`, `NestedScopes = 1`, `Globals = 2` | `None` |

`Truthiness` is a reserved public value but is not implemented by the first profile infrastructure milestone. Constructing a profile with this unsupported value must fail explicitly.

### 4.2. Future option types

Future language functionality should add independent enum types rather than members of an existing global set. Likely examples include:

| Profile property | Enum type | Initial values |
|---|---|---|
| `Comments` | `CommentsFeature` | `Disabled = 0`, `Enabled = 1` |
| `UserDefinedTypes` | `UserDefinedTypesFeature` | `Disabled = 0`, `Enabled = 1` |
| `FirstClassFunctions` | `FirstClassFunctionsFeature` | `Disabled = 0`, `Enabled = 1` |

These future options must not be added to the public `LanguageProfile` constructor until their corresponding language functionality is implemented.

## 5. Public API shape

Add:

- public `LanguageProfile`;
- the enum types listed in Section 4.1;
- `LanguageProfiles.Version1` as the standard profile;
- profile-aware compilation overloads.

Construction should validate:

- unknown enum values;
- unsupported language versions;
- option values not implemented by the selected version.

The profile exposes read-only state only and is created through one public constructor that validates the complete configuration. No builder is included initially.

## 6. Profile identity

Add a deterministic `LanguageProfileFingerprint`.

The fingerprint should include:

- language version;
- the stable numeric value of every profile option listed in Section 4.1.

The canonical fingerprint input should include both the stable option label and numeric value so adding a new property cannot be confused with reordering existing constructor parameters.

The fingerprint must not depend on enum declaration order, process-specific hashes, collection iteration order, or object identity.

Compilation cache identity becomes:

```text
source
+ compilation mode
+ language profile fingerprint
+ environment fingerprint
```

The portable IR should retain the profile fingerprint used during compilation. The .NET runtime does not need the full profile because disabled features cannot survive successful binding and lowering.

## 7. Pipeline integration

### 7.1. Lexer

The lexer receives the selected profile.

It should remain capable of recognizing tokens belonging to disabled options so later phases can report a dedicated disabled-option diagnostic.

Comments are the future exception requiring lexer-level feature handling because comment recognition changes tokenization.

### 7.2. Parser

The parser receives the selected profile and preserves recognized disabled constructs in the syntax tree where practical.

Parser-owned option checks initially include:

- `UserDefinedFunctions`;
- `OpenObjects` for `@{ ... }`;
- `Mutations.PropertyRemoval` for postfix removal syntax;
- `OptionalAccess`;
- `MultiLevelLoopControl`;
- `TrailingCommas`.

The binder remains authoritative for semantic validity even when the parser can report a disabled option directly from unmistakable syntax.

A disabled construct should produce one dedicated diagnostic at its identifying token while preserving sufficient syntax for recovery.

### 7.3. Binder

The binder is the authoritative phase for semantic feature options.

Binder-owned option checks initially include:

- `Loops`;
- `Recursion`;
- `ProviderFunctionCalls`;
- `OpenObjects`;
- `Mutations`;
- `Shadowing`;
- `ConditionSemantics`.

The binder must not rely exclusively on parser diagnostics because bound trees may later be created from alternative syntax sources.

### 7.4. Lowering and IR

Lowering should not re-evaluate feature availability.

The IR must only contain operations admitted by the selected profile.

The IR compilation unit should include the profile fingerprint so:

- cache identity is explicit;
- validators can detect incompatible compilation metadata;
- future serialized IR can retain its semantic profile.

### 7.5. Exporter and runtime

The .NET exporter should not branch on source-language feature flags.

Runtime adapters and `DotNetRuntimeContext` remain independent of the language profile.

Runtime limits such as execution budget, traversal depth, and function-call depth remain execution options rather than language features.

## 8. Diagnostics

Add a stable disabled-option diagnostic for each configurable concern containing:

- the source span of the construct;
- a concise feature-specific message.

Each concern uses its own stable diagnostic code. `LoopFeatures` and `MutationFeatures` may use distinct codes for each member when consumers need to distinguish the disabled operation directly.

Configuration-construction failures should use argument-related exceptions because they originate from host code rather than MuLang source.

Profile restrictions discovered while compiling source must use normal compilation diagnostics.

## 9. Initial options

### 9.1. `UserDefinedFunctions`

When disabled:

- `func` declarations are recognized and diagnosed;
- provider function calls remain independently configurable;
- programs without user-defined declarations are unaffected.

This flag controls declaration and invocation as one feature in the initial implementation.

### 9.2. `Recursion`

When disabled, the binder builds a directed user-function call graph and rejects every strongly connected component that represents:

- direct recursion;
- mutual recursion;
- a longer recursive cycle.

A self-edge is recursive.

The diagnostic should identify every function participating in a rejected cycle without producing duplicates for each call path.

Calls through provider functions are not considered statically recursive.

### 9.3. `Loops`

When `LoopFeatures.While` is absent:

- `while` is rejected;

When `LoopFeatures.For` is absent:

- `for` is rejected;

For either disabled loop feature:

- loop bodies are still bound for independent diagnostics;
- `break` and `continue` continue to receive their normal contextual validation.

### 9.4. `ProviderFunctionCalls`

When disabled, any call resolved to a provider function is rejected.

User-defined calls remain independently controlled by `UserDefinedFunctions` and `Recursion`.

### 9.5. `OpenObjects`

The `OpenObjects` feature controls:

- `@{ ... }` literals;
- dynamic property access;
- dynamic property assignment;
- dynamic property removal;
- provider-declared open structured types.

The generic `object` type remains available regardless of this flag. It is the abstract common supertype of all object values, analogous to `number` for numeric values and `unknown` for all non-null values.

When `OpenObjects` is disabled, a value whose static type is `object` does not expose dynamic property access, assignment, removal, or `has`. It may still be:

- assigned and passed according to the normal type rules;
- compared using structural or identity equality;
- tested or converted with `is` and `as`;
- returned from functions;
- stored in properties and arrays.

Closed structured object types remain available and retain access to their known properties.

### 9.6. `Mutations.ObjectProperties`

When disabled, assignments to object properties are rejected.

Reading properties, `has`, structural equality, object literals, and object arguments remain available.

The restriction applies to:

- object literals;
- provider objects;
- objects received through globals, parameters, properties, arrays, and function results.

### 9.7. `Mutations.ArrayElements`

When disabled, assignments to array elements are rejected.

Array literals, indexing, `length`, equality, and array arguments remain available.

### 9.8. `Mutations.PropertyRemoval`

When disabled:

- postfix `~` removal statements are rejected;
- object property assignment is unaffected unless object mutation is also disabled.

Property removal is one independently combinable member of `MutationFeatures`. The syntax remains recognizable so the compiler can report its dedicated diagnostic.

### 9.9. `OptionalAccess`

When disabled:

- `?.` is rejected;
- `?.[` is rejected;
- ordinary member and element access remain available.

Nullable values and nullable runtime checks remain part of the core type system.

### 9.10. `MultiLevelLoopControl`

When disabled:

- plain `break;` and `continue;` remain valid;
- explicitly writing a level is rejected, including level `1`;
- no change is made to the runtime lowering of the default level.

### 9.11. `TrailingCommas`

When disabled, trailing commas in:

- array literals;
- closed object literals;
- open object literals

produce a feature-disabled diagnostic.

Trailing commas in call and parameter lists remain invalid grammar rather than a configurable feature.

### 9.12. `Shadowing`

`ShadowingPolicy` uses independently combinable permissions:

- `None` preserves the current behavior and disallows all variable shadowing;
- `NestedScopes` permits a declaration in a nested lexical scope to shadow a local variable or parameter from an enclosing scope;
- `Globals` permits local variables and function parameters to shadow global variables.

Duplicate declarations in the same lexical scope remain invalid under every policy.

When only `NestedScopes` is enabled, globals cannot be shadowed.

When only `Globals` is enabled, a declaration may shadow a global but cannot shadow a local variable or parameter from an enclosing scope.

When both values are combined, both forms of shadowing are permitted.

## 10. Feature dependencies

Feature dependencies are behavioral rather than profile-construction constraints:

- `Recursion.Enabled` has an effect only when `UserDefinedFunctions.Enabled`;
- `MultiLevelLoopControl.Enabled` has an effect only when `Loops` contains at least one member;
- open object literal syntax and dynamic property operations require `OpenObjects` when encountered in source;
- `MutationFeatures.PropertyRemoval` is independent from `MutationFeatures.ObjectProperties`.

A dependent flag may remain enabled while its prerequisite is disabled. The profile remains valid and the dependent flag is dormant.

## 11. Environment compatibility

The environment fingerprint remains a description of provider symbols and structured types.

The language-profile fingerprint remains separate.

Compilation should validate environment/profile compatibility where a restriction concerns environment declarations.

Compatibility checks include:

- open structured types when open objects are disabled;
- future provider-declared feature requirements.

When `OpenObjects` is disabled, the complete environment is rejected if it contains any open structured type, including an otherwise unused type.

Provider function declarations remain compatible with a profile that disables provider calls because the restriction applies to source usage rather than environment shape.

## 12. Standard and restricted profiles

Provide one standard profile matching the full current language.

Do not initially provide many named restricted profiles. They tend to become public compatibility commitments.

Tests may define internal profiles such as:

- expression-only restricted profile;
- no-side-effects profile;
- terminating-subset profile.

Named public profiles should be introduced only when a real product scenario requires them.

## 13. Test strategy

### 13.1. Profile model

Test:

- immutability;
- default profile behavior;
- deterministic fingerprint;
- fingerprint changes for every option;
- invalid enum values;
- equality of independently constructed equivalent profiles.

### 13.2. Feature checks

For every initial flag, test:

- enabled behavior remains unchanged;
- disabled source produces the feature-disabled diagnostic;
- parser recovery continues after the construct;
- unrelated features remain usable;
- no duplicate feature diagnostics are emitted.

### 13.3. Recursion analysis

Test:

- direct recursion;
- mutual recursion;
- longer cycles;
- acyclic forward calls;
- multiple independent cycles;
- provider calls excluded from the graph;
- recursion allowed by the standard profile.

### 13.4. Option combinations

Test combinations including:

- functions enabled, recursion disabled;
- functions disabled, recursion enabled but dormant;
- `Loops = For` without `While`;
- `Loops = While` without `For`;
- `Loops = None` with multi-level control disabled;
- `Loops = None` with multi-level control enabled but dormant;
- `Mutations = ArrayElements` without `ObjectProperties`;
- `Mutations = ObjectProperties` without `PropertyRemoval`;
- open objects disabled with closed objects enabled;
- provider calls disabled with user functions enabled;
- shadowing nested variables without shadowing globals;
- shadowing globals without shadowing nested variables;
- both shadowing permissions enabled.

### 13.5. Regression

Run the entire current suite using the standard profile.

Existing public overloads without an explicit profile must remain source-compatible and behavior-compatible.

Run Debug and Release suites without warnings.

## 14. Implementation order

1. Finalize the open decisions in Section 15.
2. Define the public profile, its independent enum types, and fingerprint.
3. Add the standard version-one profile.
4. Add profile-aware compiler overloads while preserving existing overloads.
5. Thread the profile through syntax tree, lexer, parser, binder, lowering metadata, and IR.
6. Add the dedicated disabled-option diagnostics.
7. Implement parser-owned option checks.
8. Implement binder-owned option checks.
9. Implement user-function call-graph and recursion analysis.
10. Add profile/environment compatibility validation.
11. Add IR profile metadata and validation.
12. Add layered tests for each flag and combination.
13. Update the normative specification.
14. Run full Debug and Release regression validation.

## 15. Open decisions

### 15.1. Option representation

**Settled:** use one enum type per configurable concern. Use `[Flags]` when that concern contains independently combinable permissions. `LoopFeatures`, `MutationFeatures`, and `ShadowingPolicy` currently require this behavior. Do not group unrelated concerns into syntax, capability, or semantic-policy sets.

### 15.2. Loop granularity

**Settled:** use one `[Flags]` enum `LoopFeatures` with `None = 0`, `While = 1`, and `For = 2`.

### 15.3. Open-object granularity

**Settled:** use one `OpenObjects` feature controlling provider-declared open structured types, open object literals, and dynamic property operations. The generic `object` type remains always available as an abstract object supertype and exposes no dynamic members when the feature is disabled.

### 15.4. Incompatible environment declarations

**Settled:** when `OpenObjects` is disabled, compilation rejects the complete environment if any open structured type is declared, including unused types. Other options, such as provider calls, restrict source usage unless a future option explicitly defines an environment-shape incompatibility.

### 15.5. Mutation grouping

**Settled:** use one `[Flags]` enum `MutationFeatures` with independently combinable `None = 0`, `ObjectProperties = 1`, `ArrayElements = 2`, and `PropertyRemoval = 4`.

### 15.6. Feature-disabled diagnostics

**Settled:** use one stable diagnostic code per feature. Do not extend the common `Diagnostic` contract with a feature-specific property.

### 15.7. Profile construction

**Settled:** use one public constructor accepting all immutable profile values. No builder is included initially.

### 15.8. Profile placement

**Settled:** pass the profile directly to each compilation. It remains separate from `EnvironmentSchema`, and there is no environment-level default or per-compilation override hierarchy.

### 15.9. Feature dependencies

**Settled:** dependent flags may remain enabled when their prerequisites are disabled. The profile remains valid and the dependent flags are dormant.

### 15.10. Shadowing policy

**Settled:** use a `[Flags]` enum `ShadowingPolicy` with `None = 0`, `NestedScopes = 1`, and `Globals = 2`. Same-scope duplicate declarations remain invalid.

## 16. Completion criteria

The infrastructure is complete when:

- the standard profile preserves all current behavior;
- each ordinary option and each member of a combinable option can independently restrict its target behavior;
- invalid enum values and unsupported policy values fail during host-side construction;
- disabled source produces dedicated diagnostics rather than unrelated syntax errors;
- recursion-disabled profiles reject all user-function cycles;
- environment and profile fingerprints remain separate and deterministic;
- the IR records the profile fingerprint;
- the runtime remains profile-independent;
- existing compiler overloads remain compatible;
- Debug and Release test suites pass without warnings.
