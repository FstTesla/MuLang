# 9. Operators

## 9.1. General rules

Operator behavior is defined exclusively by this specification. Host-language operator rules do not affect it.

No user-defined operators exist.

The selected language profile determines whether Boolean contexts use strict Boolean conditions or truthiness as defined in [Section 14.8](14-language-profiles.md#148-conditions).

## 9.2. Precedence

Operators are listed from highest to lowest precedence.

| Category | Operators | Associativity |
|---|---|---|
| Primary | member access, element access, call | Left |
| Unary | `+`, `-`, `!`, `~` | Right |
| Multiplicative | `*`, `/`, `%` | Left |
| Additive | `+`, `-` | Left |
| Shift | `<<`, `>>` | Left |
| Conversion | `as` | Left |
| Relational and type/property tests | `<`, `<=`, `>`, `>=`, `is`, `has` | None |
| Equality | `==`, `!=`, `===`, `!==` | None |
| Bitwise AND | `&` | Left |
| Bitwise XOR | `^` | Left |
| Bitwise OR | <code>&#124;</code> | Left |
| Conditional AND | `&&` | Left |
| Conditional OR | <code>&#124;&#124;</code> | Left |
| Null coalescing | `??` | Right |
| Conditional | `? :` | Right |

The postfix property-removal token `~` is a statement terminator and is not part of expression precedence.

Non-associative operators cannot be chained at the same precedence without parentheses.

When `+` or `-` immediately precedes a numeric token or non-finite float keyword in a position where a literal is permitted, the sign is part of the literal rather than a unary expression.

> For example, multiplication binds more tightly than addition, so this expression produces `7`:
>
> ```text
> 1 + 2 * 3
> ```
>
> The following comparison is invalid because relational operators are non-associative:
>
> ```text
> 1 < 2 < 3
> ```

## 9.3. Arithmetic operators

Arithmetic operators operate on `int`, `float`, and `number`.

When both operands are `int`, the result is `int`, except where this specification explicitly requires another result.

When either concrete operand is `float`, an `int` operand is implicitly converted to `float` and the result is `float`.

When either operand has static type `number`, the other numeric operand is converted to `number` and the static result type is `number`.

Operations on values with static type `number` dispatch according to their concrete runtime kinds. Two concrete `int` operands use checked integer arithmetic and produce `int`. If either concrete operand is `float`, a concrete `int` operand is promoted to `float` and the operation produces `float`.

Integer operations are checked for overflow.

Division or remainder by integer zero produces a runtime error.

Floating-point arithmetic follows IEEE 754 binary64 semantics.

The `+` operator performs string concatenation when at least one operand has type `string` and the other operand is a primitive value, a nullable primitive value, or the `null` literal.

Non-string operands are converted using the string conversion rules before concatenation. A nullable primitive operand whose runtime value is `null` is converted to `"null"`.

Objects and arrays cannot participate in intrinsic string concatenation.

> These expressions produce `7`, `2.5`, and `"count=2"`, respectively:
>
> ```text
> 3 + 4
> ```
>
> ```text
> 1 + 1.5
> ```
>
> ```text
> "count=" + 2
> ```

## 9.4. Relational operators

Relational operators are defined for:

- numeric operands, using numeric promotion;
- string operands, using ordinal comparison.

No ordering is defined for booleans, arrays, or objects.

> String comparison is ordinal:
>
> ```text
> "A" < "a"
> ```

## 9.5. Bitwise and eager Boolean operators

`~`, `<<`, and `>>` require `int` operands and produce an `int`.

`~` performs bitwise complement.

For two `int` operands, `&`, `^`, and `|` perform bitwise AND, exclusive OR, and inclusive OR and produce an `int`.

For two `bool` operands, `&`, `^`, and `|` perform eager logical AND, exclusive OR, and inclusive OR and produce a `bool`.

Both operands of the Boolean forms are always evaluated. Unlike `&&` and `||`, `&` and `|` do not short-circuit.

Mixed `int` and `bool` operands are not permitted.

The right operand of `<<` and `>>` MUST be between 0 and 63 inclusive. A value outside this range produces a runtime error.

`<<` discards high bits and shifts zero bits into low positions.

`>>` is an arithmetic right shift and preserves the sign bit.

Bitwise operations do not perform overflow checks.

> For example, these expressions produce `8`, `3`, and `true`:
>
> ```text
> 1 << 3
> ```
>
> ```text
> 1 | 2
> ```
>
> ```text
> true ^ false
> ```

## 9.6. Checked casts, type checking, and property existence

`value as Type` performs the explicit checked cast defined in [Section 5.8](05-assignability-and-conversions.md#58-explicit-checked-casts). It returns the original value unchanged when the value conforms to the target type and otherwise produces a failed-cast runtime error.

`value is Type` evaluates to `true` when the value conforms to the specified non-void type.

Numeric conformance follows concrete representation: `int` values conform to `int` and `number`, while `float` values conform to `float` and `number`. Type tests do not apply the implicit `int`-to-`float` conversion.

When the operand's static type and the tested type cannot describe the same runtime value, the test remains valid, evaluates to `false`, and produces a warning diagnostic.

When static type shape or a safely evaluable constant operand guarantees conformance to the tested type, the test remains valid, evaluates normally, and produces a warning that its result is statically known to be `true`. Static guarantees include compatible mutable and read-only array shapes and structured object shapes even when mutation invariance prevents assignment between those types.

An `as` expression whose operand and target types are equivalent produces a redundant-cast warning. Another statically guaranteed cast produces the warning when the surrounding context already establishes an expected type that accepts both the uncast operand and the cast result. It does not produce the warning when the cast contributes to local, array-element, object-property, conditional, or other expression-type inference.

For `T[]`, conformance requires write capability and recursive conformance of every current element to `T`. For `T[]$`, read capability is sufficient. Array tests are shape-based and do not require a stored nominal element type.

For `null`, `is` evaluates to `true` only when the tested type is nullable. The `is` operator does not narrow the operand in any subsequent expression or statement.

For every statically permitted `as` expression, the corresponding `is` expression predicts its semantic success when both observe the same unchanged value. Cancellation, exhausted execution limits, incompatible environments, and host-function failures are operational failures outside this guarantee.

`target has key` checks whether an object currently contains a property. The key expression MUST have type `string`.

A test with a string literal naming a known property of a closed structured type is available in every open-objects mode. Dynamic property-existence tests require the property-existence or enabled setting defined in [Section 14.4](14-language-profiles.md#144-open-objects).

The target of `has` MUST have `object`, structured object, or a nullable form of either as its static type. A `null` target produces a runtime error.

`has` evaluates to `true` when the property is present, regardless of whether its value is `null`. It evaluates to `false` when the property is absent.

The key expression is evaluated exactly once. The `has` operator does not read the property value and does not narrow the target or property type.

> For example, a present property whose value is `null` still satisfies `has`:
>
> ```text
> var item = @{ value: null };
> return item has "value";
> ```
>
> Array conformance also observes capability:
>
> ```text
> $[1] is int[]$
> ```
>
> The preceding expression is `true`, while `$[1] is int[]` is `false`.

## 9.7. Boolean operators

Under strict Boolean condition semantics, `!`, `&&`, and `||` require Boolean operands.

Under truthiness condition semantics, each non-void operand is normalized to `bool` according to [Section 14.8](14-language-profiles.md#148-conditions) before the operator is applied.

`&&` and `||` short-circuit and evaluate operands from left to right.

`&&` and `||` always produce `bool`; they never return an operand value.

The compiler produces a warning when the result of `!`, `&&`, or `||`, or the truthiness of a relevant operand, is statically known.

> Given a host function `effect(): bool`, neither of these expressions calls it:
>
> ```text
> false && effect()
> ```
>
> ```text
> true || effect()
> ```

## 9.8. Null-coalescing operator

The null-coalescing expression `left ?? right` evaluates `left` exactly once.

Null coalescing is always available and is not controlled by the language profile.

The left operand MUST have a nullable type or be the `null` literal. The right operand MUST have a non-void type.

If the left operand is not `null`, its value is converted to the result type and returned without evaluating the right operand. Otherwise, the right operand is evaluated, converted to the result type, and returned.

For the `null` literal on the left, the result type is the type of the right operand.

For a left operand of type `T?`, the result type is the common type of non-null `T` and the right operand. The left operand does not by itself make the result nullable: the result is nullable only when the right operand and the common-type rules require it.

An expression whose non-null left value and right operand have no common result type is invalid.

For an otherwise valid null-coalescing expression, the compiler produces a warning when the left operand is statically known always to be `null` or never to be `null`. The requirement that the left operand have nullable type or be the `null` literal is unchanged.

> This expression produces `"fallback"`:
>
> ```text
> null ?? "fallback"
> ```
>
> Given `name` of type `string?`, this expression has non-null type `string`:
>
> ```text
> name ?? "anonymous"
> ```

## 9.9. Structural equality

`==` performs recursive structural equality.

`!=` is the logical negation of `==`.

For primitive values, structural equality compares values.

For `==` and `!=`, numeric operands use value equality. When an `int` is compared with a `float`, the `int` is converted to `float` before comparison. Values with static type `number` follow the same rule according to their concrete runtime kinds.

For arrays, structural equality compares lengths and corresponding elements in order.

For objects, structural equality compares the complete set of visible properties and recursively compares corresponding values. Property order is irrelevant.

Structural equality MUST safely handle cyclic object and array graphs by tracking pairs already compared.

`null` is structurally equal only to `null`.

A structural comparison with the `null` literal produces a warning when the other operand is statically known always or never to be null.

> The first expression is `true` because arrays are compared structurally. The second is `false` because the literals create distinct array identities:
>
> ```text
> [1, 2] == [1, 2]
> ```
>
> ```text
> [1, 2] === [1, 2]
> ```

## 9.10. Identity equality

`===` performs identity equality.

`!==` is the logical negation of `===`.

For primitive values with the same concrete type, identity equality coincides with value equality.

Numeric primitive values with different concrete types are never identical. In particular, an `int` and a `float` compare unequal with `===` and equal with `!==`, even when `==` considers their values equal.

For arrays and objects, identity represents the same logical value supplied through the host boundary.

A mutable array and every read-only view that forwards its logical identity are identical.

`null` is identical only to `null`.

An identity comparison with the `null` literal produces a warning when the other operand is statically known always or never to be null.

> For example, this program returns `true`:
>
> ```text
> var values = [1];
> var view: int[]$ = values;
> return values === view;
> ```
>
> Numeric representations remain distinct:
>
> ```text
> 1 == 1.0
> ```
>
> The preceding expression is `true`, while `1 === 1.0` is `false`.
