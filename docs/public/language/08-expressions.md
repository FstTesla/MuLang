# 8. Expressions

## 8.1. Expression categories

The language supports:

- literals;
- local and global variable references;
- array literals;
- object literals;
- property access;
- element access;
- function calls;
- unary operations;
- binary operations;
- null-coalescing expressions;
- conditional expressions;
- explicit checked conversions;
- runtime type checks;
- property-existence checks;
- parenthesized expressions.

Assignment and property removal are not expressions.

## 8.2. Evaluation order

Expressions are evaluated from left to right.

Function arguments are evaluated from left to right before invocation.

Property and element assignment evaluate:

1. the target;
2. the property key or array index, when computed;
3. the assigned value;
4. the mutation.

Each operand MUST be evaluated exactly once.

> Given numeric host functions `first()` and `second()`, this expression calls `first` before `second`:
>
> ```text
> first() + second()
> ```

## 8.3. Array literals

An array literal contains zero or more comma-separated expressions enclosed in square brackets.

Language version 1.1 also provides read-only array literals opened by the single `$[` token and closed by `]`.

A non-empty array literal MAY contain one trailing comma after its final expression.

The availability of trailing commas depends on the language profile as defined in [Section 14.6](14-language-profiles.md#146-trailing-commas).

All elements MUST have a common type under the implicit conversion rules. Each element is converted to that common type.

Numeric elements use the numeric conversion hierarchy to determine their common type: `int` with `float` produces `float`, while a static `number` element produces `number`.

A mixture of `T` and the `null` literal has `T?` as its common type when all non-null elements have a common non-null type `T`.

An empty array literal requires an expected array type from its context.

Normal array literals create mutable arrays. Read-only array literals create values exposing only read capability.

A read-only literal cannot be contextually converted to a mutable array. A normal literal may be contextually converted to a compatible read-only view without copying.

> This mutable literal has element type `float` because `1` is converted to `float`:
>
> ```text
> [1, 2.5]
> ```
>
> An empty literal obtains its element type from the declaration:
>
> ```text
> var values: int[] = [];
> ```
>
> This declaration is invalid because a read-only literal cannot initialize a mutable array:
>
> ```text
> var values: int[] = $[1, 2];
> ```

## 8.4. Object literals

A closed object literal contains zero or more comma-separated property initializers enclosed in `{` and `}`.

An open object literal contains the same contents enclosed in `@{` and `}`.

The availability of open object literals depends on the language profile as defined in [Section 14.4](14-language-profiles.md#144-open-objects).

A non-empty object literal MAY contain one trailing comma after its final property initializer.

The availability of trailing commas depends on the language profile as defined in [Section 14.6](14-language-profiles.md#146-trailing-commas).

Under the standard language-version-1.2 profile, each property declaration has:

- an identifier or string-literal name;
- an optional `$` read-only modifier;
- an optional `?` presence modifier;
- an optional `:` followed by an explicit type;
- an optional `=` followed by an initializer.

The canonical order is `$`, `?`, explicit type, then initializer. Required
properties require an initializer. An optional property may omit its initializer
only when it has an explicit type; omission creates no runtime property.
Optionality describes presence and remains independent from nullability.

An explicit property type controls the declared property type. A present
initializer must be implicitly assignable to it. Without an explicit type, the
initializer supplies the inferred property type; `null`, `void`, and
error-recovery values are insufficient for inference.

When contextually typed by a known structured object type, full syntax retains
the optional and read-only capabilities declared by the literal. The expected
property type guides initializer binding when no explicit source type is
present. The resulting concrete object type is then checked for ordinary
structural assignability to the expected type. Mutable target properties
therefore require matching capabilities and equivalent value types, while
read-only target properties permit representation-safe read compatibility,
including a required source property satisfying an optional target property.
An enclosing implicit conversion exposes the expected static type without
replacing the concrete capabilities of the literal.

The contextual type may be host-declared, source-declared, or inline. Source
type names do not add construction syntax or nominal conversion; the object
literal remains the constructed value and is checked structurally.

> A source declaration may provide the expected type:
>
> ```text
> type Item { name$: string, score?: int };
> var item: Item = { name$ = "MuLang", score?: int };
> ```

> This literal is invalid because the required `name` property is absent:
>
> ```text
> type Item { name: string };
> var item: Item = { };
> ```

The `ObjectLiteralSyntax.Legacy` profile setting preserves the earlier grammar,
where `:` introduces a required initializer and `?` followed by `:` introduces
an optional initializer. Legacy syntax cannot declare read-only properties or
explicit property types; contextual types continue supplying capabilities.

Computed property names and property spread are not supported.

Duplicate property names are a compile-time error.

An object literal has an inferred anonymous structured type whose known
properties correspond to its declarations. Only declarations with initializers
create present runtime properties.

A closed object literal has a closed inferred type.

An open object literal has an open inferred type and permits additional properties of type `unknown?`.

Object literals preserve the mutable or read-only capability declared for each
known property.

> This closed literal has required property `name` and optional property `score`:
>
> ```text
> var item = { name = "MuLang", score? = 1 };
> ```
>
> This open literal permits an additional property to be introduced:
>
> ```text
> var item = @{ name = "MuLang" };
> item.version = 1;
> ```

## 8.5. Property access

Dot access requires a statically known property name.

Element-style property access accepts a string expression as the property key.

Arrays support dot access only for the intrinsic `length` property.

Access to a known property has the type declared by the structured object schema.

Access to a dynamic property of an open object has type `unknown?`.

The availability of dynamic property access depends on the language profile as defined in [Section 14.4](14-language-profiles.md#144-open-objects).

A dynamic property may be present with the value `null`. Property absence remains distinct from a present null value and can be tested with `has`.

Access to an absent optional or dynamic property produces a runtime error unless optional access is used.

Access to a member through a nullable value is statically permitted but produces a runtime error when the target is `null`, unless optional access is used.

> Given a host global `item` with a known `name: string` property, dot and element access select the same property:
>
> ```text
> item.name == item["name"]
> ```

## 8.6. Optional access

Optional property access uses `target?.property`.

Optional element access uses `target?.[index]`.

Optional access is always available and is not controlled by the language profile.

When the target is `null`, optional access:

- does not evaluate the property or index operation;
- produces `null`;
- has a nullable result type.

When a dynamically named property is absent, optional access produces `null`.

Optional access to a known required property only affects a nullable target; it does not change the property schema.

Optional access to a required property or array operation produces a warning when the target is statically known never to be `null`. It is not considered redundant when it can still handle an absent optional or dynamic property. Optional access produces a warning when its target is statically known always to be `null`.

> Given `item` of a nullable structured type with a `name: string` property, this expression has type `string?` and produces `null` when `item` is `null`:
>
> ```text
> item?.name
> ```
>
> For an open object, optional access also handles an absent dynamic property:
>
> ```text
> var item = @{ };
> return item?.["missing"] ?? "fallback";
> ```

## 8.7. Array access

Array indexes have type `int`.

An index outside the valid array range produces a runtime error.

Array element access through a nullable array is statically permitted and produces a runtime error when the array is `null`, unless optional access is used.

The intrinsic `length` property returns the current number of elements as a non-negative `int`.

Access to `length` through a nullable array is statically permitted and produces a runtime error when the array is `null`. Optional access through `array?.length` produces `null` for a null array and otherwise returns its length, giving the expression type `int?`.

Array element syntax cannot be used to access `length`; array indexes always require `int`.

The intrinsic `length` property is not considered an object property and is not visible to the `has` operator.

Every array element read validates the retrieved value against the statically expected element type. A mutation through another alias that invalidates the current shape therefore causes a runtime type error on the later read.

> For example, this program produces `3`:
>
> ```text
> var values = [1, 2];
> return values[0] + values.length;
> ```
>
> This expression produces an invalid-index runtime error:
>
> ```text
> [1, 2][2]
> ```

## 8.8. Function calls

Functions may be declared by the host environment or by leading top-level `func` declarations in program mode.

User-defined function declaration, invocation, parameters, scope, returns, and recursion are specified in [Section 7](07-user-defined-functions.md). Their availability is controlled as defined in [Section 14.1](14-language-profiles.md#141-user-defined-functions-and-recursion). Calls to host-provided functions are independently controlled as defined in [Section 14.3](14-language-profiles.md#143-host-provided-function-calls).

Function names are resolved only in call position.

Functions are synchronous, cannot be overloaded, and require exactly the declared number of arguments.

User-defined functions require explicit parameter and return types. They may return `void`, support forward calls and recursion, and are not first-class values.

The availability of recursion depends on the language profile as defined in [Section 14.1](14-language-profiles.md#141-user-defined-functions-and-recursion).

User-defined function names MUST NOT conflict with host-provided function names. User-defined functions cannot be nested.

Arguments MUST be assignable to their corresponding parameter types.

A void-returning call can only be used as a call statement.

A non-void call MAY be used as an expression or discarded as a call statement.

Every function call is potentially effectful. Its evaluation MUST NOT be omitted, duplicated, or reordered relative to other observable operations.

> Given a host function `parse(string): int`, this is a non-void call expression:
>
> ```text
> parse("42") + 1
> ```

## 8.9. Nullable operands

MuLang does not perform flow-sensitive narrowing. A type test or null check does not change the static type of a variable in a later expression or statement.

Operations whose operand type is nullable are statically permitted when the corresponding non-null type supports the operation.

The operation MUST validate the operand at runtime and produce a MuLang runtime error when a required operand is `null`.

Comparisons explicitly defined for `null` do not require non-null operands.

Null coalescing is explicitly defined for nullable operands in [Section 9.8](09-operators.md#98-null-coalescing-operator).

> For example, the type test does not narrow `value`; the checked cast remains required:
>
> ```text
> var value: unknown = 1;
> if (value is int) {
>     return value as int;
> }
> return 0;
> ```

## 8.10. Conditional expression

The conditional expression is right-associative and has lower precedence than every other expression operator.

Its condition MUST satisfy the selected condition semantics in [Section 14.8](14-language-profiles.md#148-conditions).

Its branches MUST have a common type according to the language conversion rules.

Only the selected branch is evaluated.

> This expression produces `1` without evaluating the division by zero:
>
> ```text
> true ? 1 : 1 / 0
> ```

## 8.11. Compile-time constant evaluation

When compile-time constant evaluation is enabled by the selected language profile, a constant primitive expression MUST be evaluated during compilation and replaced by its result. Compile-time evaluation MUST use the same value, conversion, operator, and failure semantics as ordinary execution.

A constant primitive expression is a primitive literal or an expression whose required operands have been reduced to primitive literals. Compile-time evaluation applies to:

- intrinsic unary and binary primitive operators;
- primitive value conversions and checked casts;
- primitive type tests and truthiness normalization;
- null coalescing;
- conditional expressions;
- conditional Boolean operators.

Compile-time evaluation MUST preserve source evaluation order and short-circuit behavior. An operand or conditional branch that is known not to be evaluated MUST NOT be evaluated and MUST NOT produce a constant-evaluation diagnostic.

Host-provided calls, user-defined calls, global or local reads, array and object creation, member access, and element access are not constant expressions. Constant subexpressions within them MAY still be evaluated during compilation.

If evaluating a required constant subexpression produces integer overflow, integer division or remainder by zero, an invalid shift count, or a failed checked cast, compilation MUST report an error and MUST NOT produce an executable result.

When compile-time constant evaluation is disabled, these expressions are evaluated only during execution. A statically determined evaluation failure produces a compile-time warning and remains a runtime error if execution reaches the expression.

> With constant evaluation enabled, this expression produces a compile-time division-by-zero error:
>
> ```text
> 1 / 0
> ```
>
> The short-circuited expression remains valid because its right operand is not required:
>
> ```text
> false && 1 / 0 == 0
> ```
