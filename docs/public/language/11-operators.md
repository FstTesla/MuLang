# 11. Operators

## 11.1. General rules

Operator behavior is defined by MuLang types and MUST NOT be delegated directly to host-runtime operator resolution.

No user-defined operators exist.

The selected language profile determines whether Boolean contexts use strict Boolean conditions or truthiness as defined in [Section 18.9](18-language-profiles.md#189-conditions).

## 11.2. Precedence

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

When `+` or `-` immediately precedes a numeric token in a literal position, the parser forms a signed numeric literal rather than a unary expression.

## 11.3. Arithmetic operators

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

## 11.4. Relational operators

Relational operators are defined for:

- numeric operands, using numeric promotion;
- string operands, using ordinal comparison.

No ordering is defined for booleans, arrays, or objects.

## 11.5. Bitwise and eager Boolean operators

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

## 11.6. Type conversion, type checking, and property existence

`value as Type` performs the explicit checked conversion defined in [Section 8.8](08-assignability-and-conversions.md#88-explicit-checked-conversions).

`value is Type` evaluates to `true` when the runtime value conforms to the specified non-void type.

For `T[]`, conformance requires mutable runtime capability and recursive conformance of every current element to `T`. For `T[]$`, read capability is sufficient. Array tests are shape-based and do not require a reified nominal element type.

For `null`, `is` evaluates to `true` only when the tested type is nullable. The `is` operator does not narrow the operand in any subsequent expression or statement.

`target has key` checks whether an object currently contains a property. The key expression MUST have type `string`.

A test with a string literal naming a known property of a closed structured type is available in every open-objects mode. Dynamic property-existence tests require `OpenObjects.PropertyExistenceOnly` or `OpenObjects.Enabled`, as defined in [Section 18.5](18-language-profiles.md#185-open-objects).

The target of `has` MUST have `object`, structured object, or a nullable form of either as its static type. A `null` target produces a runtime error.

`has` evaluates to `true` when the property is present, regardless of whether its value is `null`. It evaluates to `false` when the property is absent.

The key expression is evaluated exactly once. The `has` operator does not read the property value and does not narrow the target or property type.

## 11.7. Boolean operators

Under strict Boolean condition semantics, `!`, `&&`, and `||` require Boolean operands.

Under truthiness condition semantics, each non-void operand is normalized to `bool` according to [Section 18.9](18-language-profiles.md#189-conditions) before the operator is applied.

`&&` and `||` short-circuit and evaluate operands from left to right.

`&&` and `||` always produce `bool`; they never return an operand value.

## 11.8. Null-coalescing operator

The null-coalescing expression `left ?? right` evaluates `left` exactly once.

Null coalescing is always available and is not controlled by the language profile.

The left operand MUST have a nullable type or be the `null` literal. The right operand MUST have a non-void type.

If the left operand is not `null`, its value is converted to the result type and returned without evaluating the right operand. Otherwise, the right operand is evaluated, converted to the result type, and returned.

For the `null` literal on the left, the result type is the type of the right operand.

For a left operand of type `T?`, the result type is the common type of non-null `T` and the right operand. The left operand does not by itself make the result nullable: the result is nullable only when the right operand and the common-type rules require it.

An expression whose non-null left value and right operand have no common result type is invalid.

## 11.9. Structural equality

`==` performs recursive structural equality.

`!=` is the logical negation of `==`.

For primitive values, structural equality compares values.

For `==` and `!=`, numeric operands use value equality. When an `int` is compared with a `float`, the `int` is converted to `float` before comparison. Values with static type `number` follow the same rule according to their concrete runtime kinds.

For arrays, structural equality compares lengths and corresponding elements in order.

For objects, structural equality compares the complete set of visible properties and recursively compares corresponding values. Property order is irrelevant.

Structural equality MUST safely handle cyclic object and array graphs by tracking pairs already compared.

`null` is structurally equal only to `null`.

## 11.10. Identity equality

`===` performs identity equality.

`!==` is the logical negation of `===`.

For primitive values with the same concrete type, identity equality coincides with value equality.

Numeric primitive values with different concrete types are never identical. In particular, an `int` and a `float` compare unequal with `===` and equal with `!==`, even when `==` considers their values equal.

For arrays and objects, identity is supplied by the runtime adapter and represents the same logical runtime instance.

A mutable array and every read-only view that forwards its logical identity are identical.

`null` is identical only to `null`.
