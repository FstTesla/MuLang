# 18. Language profiles

Every compilation MUST select a language profile independently from its static environment and compilation mode.

A language profile is immutable and contains:

- a language version;
- one independently typed enum value for each configurable concern;
- a deterministic language-profile fingerprint.

The standard profile for language version 1 preserves all language syntax, capabilities, and policies described by the rest of this specification unless this section explicitly permits a restriction. It enables user-defined functions, recursion, both loop kinds, provider function calls, open objects, every mutation kind, explicit loop-control levels, trailing commas, and compile-time constant folding. It uses strict Boolean conditions and prohibits variable shadowing.

The standard profile for language version 1.1 has the same configurable feature defaults and adds read-only array types and literals together with the `infty` and `nan` float literals as unconditional language features. Profiles for language version 1 reject `$` and `$[` with a language-version diagnostic and treat `infty` and `nan` as identifiers.

Language version 1.1 is the default for compiler, lexer, parser, profile-builder, and environment-builder APIs. Hosts MAY select language version 1 explicitly.

The profile options and stable numeric values are:

| Property | Values | Combinable |
|---|---|---|
| `UserDefinedFunctions` | `Disabled = 0`, `Enabled = 1` | No |
| `Recursion` | `Disabled = 0`, `Enabled = 1` | No |
| `Loops` | `None = 0`, `While = 1`, `For = 2` | Yes |
| `ProviderFunctionCalls` | `Disabled = 0`, `Enabled = 1` | No |
| `OpenObjects` | `Disabled = 0`, `PropertyExistenceOnly = 1`, `Enabled = 2` | No |
| `Mutations` | `None = 0`, `ObjectProperties = 1`, `ArrayElements = 2`, `PropertyRemoval = 4` | Yes |
| `MultiLevelLoopControl` | `Disabled = 0`, `Enabled = 1` | No |
| `TrailingCommas` | `Disabled = 0`, `Enabled = 1` | No |
| `ConstantFolding` | `Disabled = 0`, `Enabled = 1` | No |
| `ConditionSemantics` | `StrictBoolean = 0`, `Truthiness = 1` | No |
| `Shadowing` | `None = 0`, `NestedScopes = 1`, `Globals = 2` | Yes |

Both condition-semantics values are supported in language versions 1 and 1.1. Profile construction MUST reject unknown enum values, unknown flag bits, and unsupported language versions. A dependent option MAY be enabled while its prerequisite is disabled; it remains dormant rather than making the profile invalid.

## 18.1. Profile fingerprint and compilation identity

The language-profile fingerprint MUST be deterministic. Its canonical input MUST include the stable label and numeric value of the language version and every profile option. It MUST NOT depend on enum declaration order, collection iteration order, process-specific hashes, or object identity.

Compilation cache identity consists of:

- source text;
- compilation mode;
- language-profile fingerprint;
- environment fingerprint.

The language-profile and environment fingerprints remain separate.

## 18.2. User-defined functions and recursion

When user-defined functions are disabled, function declarations remain recognizable for recovery but are rejected with a dedicated diagnostic. Calls resolved to a user-defined function are also rejected. Provider calls remain independently configurable.

When recursion is disabled and user-defined functions are enabled, the compiler MUST reject every strongly connected component in the user-function call graph that contains more than one function or a self-edge. Each participating function receives one useful diagnostic without diagnostics for every possible call path. Provider calls do not create recursion edges.

The recursion option is dormant when user-defined functions are disabled.

## 18.3. Loops and loop control

`Loops.While` independently permits `while`, and `Loops.For` independently permits `for`. A disabled loop body MUST still be analyzed for independent diagnostics and normal `break` and `continue` context validation.

When multi-level loop control is disabled, explicitly written levels on `break` and `continue` are rejected, including level `1`. Plain `break;` and `continue;` remain available. The option is dormant when `Loops` is `None`.

## 18.4. Provider calls

When provider function calls are disabled, a call resolved to a provider function is rejected. Provider function declarations remain valid in the environment and may be unused.

## 18.5. Open objects

The open-objects option has three ordered capability levels:

| Capability | `Disabled` | `PropertyExistenceOnly` | `Enabled` |
|---|---|---|---|
| `has` with a literal key naming a known property of a closed structured type | Yes | Yes | Yes |
| Dynamic `has` tests | No | Yes | Yes |
| `@{ ... }` literals | No | No | Yes |
| Provider-declared open structured types | No | No | Yes |
| Dynamic member and element access | No | No | Yes |
| Dynamic property assignment and removal | No | No | Yes |

`PropertyExistenceOnly` permits `has` with a computed key on a closed structured type and permits `has` on the generic `object` type. It does not permit reading the selected property value.

Both `Disabled` and `PropertyExistenceOnly` reject an environment containing any open structured type, including an unused type.

The generic `object` type remains a valid abstract supertype in every mode. With `Disabled`, it exposes no dynamic operations. With `PropertyExistenceOnly`, it exposes only `has`. It remains valid in every mode for assignment, argument passing, return values, arrays and properties, equality, identity equality, `is`, and `as`.

Closed structured types remain valid. Their statically known properties remain accessible.

## 18.6. Mutations

`Mutations.ObjectProperties` independently permits assignment to an object property through member or element syntax.

`Mutations.ArrayElements` independently permits assignment to an array element.

`Mutations.PropertyRemoval` independently permits postfix `~` property-removal statements. Property removal does not depend on object-property assignment.

Mutation restrictions apply regardless of whether the value originated from a literal, global, parameter, property, array element, or function result.

## 18.7. Trailing commas

When trailing commas are disabled, a trailing comma is rejected in array, closed-object, and open-object literals. Trailing commas in argument and parameter lists remain unconditional grammar errors.

## 18.8. Shadowing

Same-scope duplicate declarations are invalid under every shadowing policy.

`Shadowing.NestedScopes` permits a declaration in a nested lexical scope to shadow an enclosing local variable or function parameter. It does not permit shadowing a global.

`Shadowing.Globals` permits locals and function parameters to shadow globals. It does not permit shadowing enclosing locals or parameters.

Combining both values permits both forms. `None` prohibits both and preserves the standard policy for language version 1.

## 18.9. Conditions

`ConditionSemantics.StrictBoolean` requires `if`, `while`, `for`, and conditional-expression conditions to have type `bool`. It also requires `!`, `&&`, and `||` operands to have type `bool`. Non-Boolean values produce the existing type or operator diagnostic.

`ConditionSemantics.Truthiness` accepts every non-void type in those contexts and normalizes the value to `bool` with these rules:

| Runtime value | Boolean result |
|---|---|
| `null` | `false` |
| `false` | `false` |
| `int` zero | `false` |
| `float` positive zero, negative zero, or NaN | `false` |
| empty `string` | `false` |
| every other supported value | `true` |

A value with static type `number` dispatches according to its concrete runtime `int` or `float` representation. Values with static type `unknown` or `unknown?` dispatch according to their concrete runtime value. Every non-null object and array is truthy, including empty values.

Truthiness is contextual. It does not add an implicit conversion to `bool`, a source-level Boolean cast, flow-sensitive narrowing, or changes to equality and identity. `void` remains invalid. Unsupported runtime representations produce a MuLang invalid-runtime-value error.

The standard `LanguageProfiles.Version1` profile uses `StrictBoolean`.

The standard `LanguageProfiles.Version1_1` profile also uses `StrictBoolean`.

## 18.10. Compile-time constant folding

`ConstantFoldingFeature.Enabled` performs the compile-time simplification defined in [Section 10.11](10-expressions.md#1011-compile-time-constant-evaluation) and reports failures in required constant expressions during compilation.

`ConstantFoldingFeature.Disabled` bypasses the constant-folding pass. Expressions retain their ordinary runtime evaluation and failures remain runtime errors.

Both standard language profiles enable compile-time constant folding.

The option contributes to the language-profile fingerprint and therefore to compilation cache identity.

## 18.11. Diagnostics

Each disabled feature or independently controllable member MUST use its dedicated stable diagnostic code. Diagnostics do not carry separate feature metadata. Recognized disabled syntax SHOULD be retained sufficiently for later phases to recover and report independent diagnostics.
