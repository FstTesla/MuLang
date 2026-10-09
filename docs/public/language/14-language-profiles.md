# 14. Language profiles

Every compilation MUST select a language profile independently from its static environment and compilation mode. The selected settings remain fixed for that compilation and its execution.

A language profile selects:

- a language version;
- whether user-defined functions are enabled;
- whether user-defined object types are enabled;
- whether recursion is enabled;
- which loop forms are enabled;
- whether calls to host-provided functions are enabled;
- the available open-object operations;
- the available mutation operations;
- whether explicit multi-level loop control is enabled;
- whether trailing commas are enabled;
- whether compile-time constant evaluation is enabled;
- whether source-level exception handling is enabled;
- the object-literal property grammar;
- the condition semantics;
- the permitted forms of variable shadowing.

Unknown settings and unsupported language versions MUST be rejected. A setting whose prerequisite is disabled remains dormant rather than making the profile invalid.

The standard profile for language version 1 enables user-defined functions,
recursion, both loop kinds, calls to host-provided functions, open objects,
every mutation kind, explicit loop-control levels, trailing commas, and
compile-time constant evaluation. It disables user-defined object types, uses
strict Boolean conditions, and prohibits variable shadowing.

The standard profile for language version 1.1 has the same settings and adds read-only array types and literals, binary, octal, and hexadecimal integer literals, and the `infty` and `nan` float literals. These additions are determined by the language version and are not independently configurable.

The standard profile for language version 1.2 enables user-defined object
types and source-level exception handling, and adds the `primitive` abstract
type and full object-literal property grammar.

Language version 1 rejects `$`, `$[`, and prefixed integer literals and treats `infty` and `nan` as identifiers.

Language versions 1 and 1.1 treat `catch`, `finally`, `primitive`, `throw`,
`type`, and `try` as identifiers and warn that the spellings become reserved
in version 1.2.

> For example, this expression is valid in the standard language-version-1.1 profile and rejected as an unavailable feature in language version 1:
>
> ```text
> $[1, 2]
> ```

## 14.1. User-defined functions and recursion

The language semantics of user-defined functions are defined in [Section 7](07-user-defined-functions.md).

When user-defined functions are disabled, a function declaration and a call resolved to a user-defined function are compile-time errors. Calls to host-provided functions remain independently configurable.

When recursion is disabled, no user-defined function may call itself directly or participate in a cycle of calls among user-defined functions.

Calls to host-provided functions do not create recursion among user-defined functions. The recursion setting is dormant when user-defined functions are disabled.

> When recursion is disabled, this declaration produces a compile-time error:
>
> ```text
> func recurse(): int {
>     return recurse();
> }
> ```

## 14.2. Loops and loop control

The profile independently permits `while` statements and `for` statements.

When a loop form is disabled, use of that loop form is a compile-time error.

When explicit multi-level loop control is disabled, a numeric level written on `break` or `continue` is a compile-time error, including level `1`. Plain `break;` and `continue;` remain available within enabled loops.

The multi-level loop-control setting is dormant when both loop forms are disabled.

> When explicit multi-level loop control is disabled, `break;` remains valid but the explicit level in this loop produces a compile-time error:
>
> ```text
> while (true) {
>     break 1;
> }
> ```

## 14.3. Host-provided function calls

When calls to host-provided functions are disabled, a call resolved to such a function is a compile-time error.

Host-provided function declarations remain valid in the static environment and need not be used by the source program.

> Given a host function `log(int): void`, this statement is rejected when host-provided calls are disabled:
>
> ```text
> log(1);
> ```

## 14.4. Open objects

The open-objects setting has three ordered capability levels:

| Capability | Disabled | Property existence | Enabled |
|---|---|---|---|
| `has` with a literal key naming a known property of a closed structured type | Yes | Yes | Yes |
| Dynamic `has` tests | No | Yes | Yes |
| `@{ ... }` literals | No | No | Yes |
| Host-declared open structured types | No | No | Yes |
| Dynamic member and element access | No | No | Yes |
| Dynamic property assignment and removal | No | No | Yes |

The property-existence setting permits `has` with a computed key on a closed structured type and permits `has` on the generic `object` type. It does not permit reading the selected property value.

The disabled and property-existence settings reject a static environment containing an open structured type, including an unused type.

The generic `object` type remains a valid abstract supertype under every setting. When open objects are disabled, it exposes no dynamic operations. Under the property-existence setting, it exposes only `has`.

In every setting, `object` remains valid for assignment, argument passing, return values, arrays and properties, equality, identity equality, `is`, and `as`.

Closed structured types and their statically known properties remain available under every setting.

> Under the property-existence setting, this expression may be valid for `item: object`:
>
> ```text
> item has "name"
> ```
>
> The corresponding value read remains unavailable:
>
> ```text
> item["name"]
> ```

## 14.5. Mutations

The profile independently controls:

- assignment to object properties;
- assignment to array elements;
- property removal.

Property removal does not depend on object-property assignment.

Mutation restrictions apply regardless of whether a value originated from a literal, global, parameter, property, array element, or function result.

> When array-element assignment is disabled, this statement produces a compile-time error even though the array originated from a mutable literal:
>
> ```text
> var values = [1];
> values[0] = 2;
> ```

## 14.6. Trailing commas

When trailing commas are disabled, a trailing comma is a compile-time error in array, closed-object, and open-object literals.

Trailing commas in argument and parameter lists are always invalid.

> When trailing commas are enabled, this literal is valid:
>
> ```text
> [1, 2,]
> ```
>
> The call `log(1, 2,)` remains invalid in every profile.

## 14.7. Shadowing

A duplicate declaration in the same scope is invalid under every shadowing policy.

Nested-scope shadowing permits a declaration in a nested lexical scope to shadow an enclosing local variable or function parameter. It does not permit shadowing a global.

Global shadowing permits locals and function parameters to shadow globals. It does not permit shadowing enclosing locals or parameters.

The two permissions MAY be combined. When neither is selected, both forms of shadowing are prohibited.

> When nested-scope shadowing is enabled, this program is valid:
>
> ```text
> var value = 1;
> {
>     var value = 2;
> }
> return value;
> ```

## 14.8. Conditions

Under strict Boolean semantics, `if`, `while`, `for`, and conditional-expression conditions MUST have type `bool`. The operands of `!`, `&&`, and `||` MUST also have type `bool`.

Under truthiness semantics, every non-void type is accepted in those contexts and its value is normalized to `bool` as follows:

| Value | Boolean result |
|---|---|
| `null` | `false` |
| `false` | `false` |
| `int` zero | `false` |
| `float` positive zero, negative zero, or NaN | `false` |
| empty `string` | `false` |
| every other supported value | `true` |

A value with static type `number` is interpreted according to its concrete `int` or `float` representation. A value with static type `unknown` or `unknown?` is interpreted according to its concrete value. Every non-null object and array is truthy, including an empty value.

Truthiness is contextual. It does not add an implicit conversion to `bool`, a source-level Boolean cast, flow-sensitive narrowing, or changes to equality and identity. `void` remains invalid. A value that cannot be represented by any supported MuLang type produces a runtime error.

All standard language profiles use strict Boolean semantics.

> Under strict Boolean semantics, this condition is invalid:
>
> ```text
> if (1) {
>     return 1;
> }
> return 0;
> ```
>
> Under truthiness semantics, it is valid and returns `1`. Empty arrays are also truthy when their element type is established by context:
>
> ```text
> var values: int[] = [];
> if (values) {
>     return 1;
> }
> return 0;
> ```

## 14.9. Compile-time constant evaluation

When enabled, compile-time constant evaluation follows [Section 8.11](08-expressions.md#811-compile-time-constant-evaluation).

When disabled, the same expressions retain their ordinary execution-time behavior, and failures that occur while evaluating them are runtime errors.

All standard language profiles enable compile-time constant evaluation.

> With constant evaluation enabled, `1 / 0` is a compile-time error. With it disabled, the same expression compiles and produces a runtime error when executed.

## 14.10. Object literal syntax

`ObjectLiteralSyntax.Legacy` selects the initializer-only `:` and `?:` grammar.
`ObjectLiteralSyntax.Full` selects the language-version-1.2 declaration grammar
with `$`, `?`, explicit types, and `=` initializers.

The setting is dormant before language version 1.2, where parsing always uses
the legacy grammar. The standard profiles for versions 1 and 1.1 use `Legacy`;
the standard version-1.2 profile uses `Full`. Legacy mode is a migration option,
not the standard version-1.2 grammar.

The configured value contributes to the language-profile fingerprint even while
dormant. Syntax from the unselected mode produces a targeted diagnostic.

## 14.11. Exception handling

The exception-handling setting controls protected statements, explicit
throwing, and rethrow. It is dormant before language version 1.2 because those
versions do not define the syntax.

The standard version-1.2 profile enables exception handling. A customized
version-1.2 profile may disable it while retaining the built-in `error` type,
which remains available for host values and portable IR contracts.

> Given host functions `update(): void` and `recover(): void`, this statement is
> valid when exception handling is enabled:
>
> ```text
> try {
>     update();
> } catch {
>     recover();
> }
> ```

> The same statement produces an unavailable-feature diagnostic when exception
> handling is disabled. The following declaration remains valid in that
> profile because the `error` type is independently available. Given a host
> global `currentFailure: error`:
>
> ```text
> var failure: error = currentFailure;
> ```

## 14.12. User-defined object types

`UserDefinedTypesFeature` controls top-level object type declarations and
inline object type bodies together. The setting is effective in language
version 1.2 and dormant in earlier versions, where `type` remains an identifier
and object bodies are not primary type syntax.

The standard version-1.2 profile enables the feature. A customized profile may
disable it; declarations and inline bodies still parse and each occurrence
produces a targeted feature diagnostic rather than unrelated syntax errors.
Open source and inline types additionally require the open-objects setting.

The configured value contributes to the language-profile fingerprint,
including while dormant.

> With the feature enabled, this declaration and inline annotation are valid:
>
> ```text
> type Named { value: int };
> var value: { value: int } = { value: int = 1 };
> ```

## 14.13. Feature diagnostics

Use of syntax or behavior disabled by the selected profile is a compile-time error.
