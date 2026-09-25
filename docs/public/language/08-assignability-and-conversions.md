# 8. Assignability and conversions

## 8.1. Identity conversion

A value is assignable to the same type.

## 8.2. Nullable conversion

A value of type `T` is assignable to `T?`.

The `null` literal is assignable to every nullable type and to no non-null type.

A value of type `T?` is not implicitly assignable to `T`.

## 8.3. Unknown conversion

Every non-null type is implicitly assignable to `unknown`.

Every type, including nullable types, is implicitly assignable to `unknown?`.

Conversion from `unknown` or `unknown?` to a more specific type requires an explicit checked conversion.

## 8.4. Numeric conversion

`int` is implicitly convertible to `float`.

`int` and `float` are implicitly convertible to `number`.

The implicit `int`-to-`float` conversion changes the runtime representation. Runtime conformance and checked casts do not apply this conversion.

A value of static type `number` may be checked-cast to `int` or `float`. The cast succeeds only when the concrete runtime representation already matches the target type and returns the original value unchanged.

There is no checked cast from a statically known `int` to `float` or from a statically known `float` to `int`.

Integer arithmetic is checked. Overflow produces a runtime error.

## 8.5. String conversion

Primitive values and `null` are contextually converted to `string` by string concatenation.

The `as` operator does not perform string conversion. A checked cast to `string` only validates that a value whose static type does not determine its runtime representation, such as `unknown`, already contains a string.

The result uses the culture-independent source representation of the value:

- `null`, `true`, and `false` use their corresponding keyword spelling;
- an `int` uses signed invariant decimal notation;
- a finite `float` uses the shortest round-trip decimal representation accepted by the float-literal grammar;
- a `number` uses the representation of its concrete runtime kind;
- positive infinity, negative infinity, and NaN use `Infinity`, `-Infinity`, and `NaN`, respectively;
- a `string` is unchanged and is not surrounded by quotes or escaped.

`Infinity`, `-Infinity`, and `NaN` are runtime string representations and are not valid source literals.

Objects and arrays have no intrinsic string conversion.

## 8.6. Object conversion

Every non-null structured object type is assignable to `object`.

Assignment between structured object types is structural and requires compatibility of known properties, optionality, and openness.

Because structured objects are mutable, structural object types are invariant. Assignment requires the same openness and the same set of known properties, with equivalent property types and matching optionality. Type names and provider identifiers do not affect structural compatibility.

The precise structural compatibility algorithm is part of the type system and MUST NOT depend on host-runtime class inheritance.

## 8.7. Array conversion

Mutable array types are invariant.

An array of `T` is not implicitly assignable to an array of another element type, including `unknown[]`, unless the two array types are equivalent.

This rule prevents writes through a widened mutable array reference from violating the original element type.

Language version 1.1 permits an array of `S` to be observed as `T[]$` when `S` is representation-safe for reads as `T`. The relation permits equivalent types, non-null values to `unknown`, nullable lifting, `int` or `float` to `number`, structured objects to `object`, and recursively compatible read-only array views. It does not permit representation-changing conversions such as `int` to `float`.

Both `S[]` and `S[]$` may convert implicitly to a compatible `T[]$`. A read-only array is never implicitly assignable to a mutable array.

Common array types preserve a mutable type only for equivalent mutable arrays. Compatible mutable and read-only operands otherwise use the least compatible read-only array view.

## 8.8. Explicit checked casts

An explicit checked cast uses the infix `as` operator followed by a non-void type.

A checked cast validates runtime conformance when the source type does not already prove success and never changes the runtime value or its representation. An implementation SHOULD omit the runtime conformance operation when static assignability proves that the cast must succeed.

A cast is statically permitted when the source and target types can describe the same runtime value. This includes removal of nullability, refinement from `unknown`, concrete-kind checks from `number`, structural object checks, and array shape or capability checks.

Representation-changing operations are not checked casts. In particular, the implicit conversion from `int` to `float` and the contextual conversion to `string` cannot be requested with `as`.

A failed checked cast produces a MuLang runtime error at the `as` expression source span.

For every statically permitted `value as Type`, and excluding operational failures, `value is Type` evaluates to `true` if and only if the corresponding `as` expression completes successfully when applied to the same unchanged runtime value.

Recursive runtime conformance MUST safely handle cyclic object and array graphs by tracking active logical identities and target types. Revisiting an active identity under a target already implied by its active target type succeeds coinductively.

An `as` expression has the target type. It does not change the static type of the original variable or expression elsewhere in the program.

The `as` operator is left-associative.

Array checked casts are shape-based. A cast to `T[]` requires runtime write capability and current recursive conformance of every element to `T`. A cast to `T[]$` requires read capability and the same element conformance. A successful cast preserves identity and does not establish a permanent invariant against later mutation through another alias.
