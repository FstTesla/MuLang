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

`number` is explicitly convertible to `int` and `float`. The conversion validates the concrete runtime numeric kind and value.

An explicit conversion from `number` to `int` MUST fail at runtime when its concrete value is a `float` that is not finite, is not integral, or is outside the signed 64-bit range.

An explicit conversion from `number` to `float` converts a concrete `int` value to `float` and preserves a concrete `float` value.

There is no conversion from `float` to `int`.

Integer arithmetic is checked. Overflow produces a runtime error.

## 8.5. String conversion

Primitive values and `null` can be converted to `string`.

The conversion is explicit except when performed implicitly by string concatenation.

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

Language version 2 permits an array of `S` to be observed as `T[]$` when `S` is representation-safe for reads as `T`. The relation permits equivalent types, non-null values to `unknown`, nullable lifting, `int` or `float` to `number`, structured objects to `object`, and recursively compatible read-only array views. It does not permit representation-changing conversions such as `int` to `float`.

Both `S[]` and `S[]$` may convert implicitly to a compatible `T[]$`. A read-only array is never implicitly assignable to a mutable array.

Common array types preserve a mutable type only for equivalent mutable arrays. Compatible mutable and read-only operands otherwise use the least compatible read-only array view.

## 8.8. Explicit checked conversions

An explicit conversion uses the infix `as` operator followed by a non-void type.

A checked conversion performs runtime validation when static validation cannot prove success.

A failed checked conversion produces a MuLang runtime error at the `as` expression source span.

The language does not provide a general predicate that determines whether an `as` conversion will succeed. The `is` operator tests runtime assignability, which is intentionally distinct from convertibility. In particular, a value can be convertible to a type without being assignable to that type.

An `as` expression has the target type. It does not change the static type of the original variable or expression elsewhere in the program.

The `as` operator is left-associative.

Array checked conversions are shape-based. Conversion to `T[]` requires runtime write capability and current recursive conformance of every element to `T`. Conversion to `T[]$` requires read capability and the same element conformance. A successful conversion preserves identity and does not establish a permanent invariant against later mutation through another alias.
