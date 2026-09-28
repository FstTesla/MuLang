# 5. Assignability and conversions

## 5.1. Identity conversion

A value is assignable to the same type.

## 5.2. Nullable conversion

A value of type `T` is assignable to `T?`.

The `null` literal is assignable to every nullable type and to no non-null type.

A value of type `T?` is not implicitly assignable to `T`.

> For example, removing nullability requires a checked cast:
>
> ```text
> var possible: int? = 1;
> var definite: int = possible as int;
> ```

## 5.3. Unknown conversion

Every non-null type is implicitly assignable to `unknown`.

Every type, including nullable types, is implicitly assignable to `unknown?`.

Conversion from `unknown` or `unknown?` to a more specific type requires an explicit checked conversion.

> ```text
> var source: unknown? = "value";
> var text: string = source as string;
> ```

## 5.4. Numeric conversion

`int` is implicitly convertible to `float`.

`int` and `float` are implicitly convertible to `number`.

The implicit `int`-to-`float` conversion changes the runtime representation. Runtime conformance and checked casts do not apply this conversion.

A value of static type `number` may be checked-cast to `int` or `float`. The cast succeeds only when the concrete runtime representation already matches the target type and returns the original value unchanged.

There is no checked cast from a statically known `int` to `float` or from a statically known `float` to `int`.

Integer arithmetic is checked. Overflow produces a runtime error.

> The first declaration applies an implicit representation-changing conversion. The second only widens the static type:
>
> ```text
> var floating: float = 1;
> var numeric: number = floating;
> ```
>
> The following checked cast succeeds only when `numeric` currently contains an `int` representation:
>
> ```text
> var numeric: number = 1;
> var integer: int = numeric as int;
> ```

## 5.5. String conversion

Primitive values and `null` are contextually converted to `string` by string concatenation.

The `as` operator does not perform string conversion. A checked cast to `string` only validates that a value whose static type does not determine its runtime representation, such as `unknown`, already contains a string.

The result uses the culture-independent source representation of the value:

- `null`, `true`, and `false` use their corresponding keyword spelling;
- an `int` uses signed invariant decimal notation;
- a finite `float` uses the shortest round-trip decimal representation accepted by the float-literal grammar;
- a `number` uses the representation of its concrete runtime kind;
- positive infinity, negative infinity, and NaN use `infty`, `-infty`, and `nan`, respectively;
- a `string` is unchanged and is not surrounded by quotes or escaped.

The non-finite representations are valid language version 1.1 source literals.

Objects and arrays have no intrinsic string conversion.

> For example, this expression produces `"value=42, missing=null"`:
>
> ```text
> "value=" + 42 + ", missing=" + null
> ```
>
> This expression is invalid because arrays have no intrinsic string conversion:
>
> ```text
> "values=" + [1, 2]
> ```

## 5.6. Object conversion

Every non-null structured object type is assignable to `object`.

Assignment between structured object types is structural and requires compatibility of known properties, optionality, and openness.

Because structured objects are mutable, structural object types are invariant. Assignment requires the same openness and the same set of known properties, with equivalent property types and matching optionality. Type names and host-defined identifiers do not affect structural compatibility.

Structural compatibility depends only on the MuLang type shapes described above. Host type identity and inheritance do not affect it.

## 5.7. Array conversion

Mutable array types are invariant.

An array of `T` is not implicitly assignable to an array of another element type, including `unknown[]`, unless the two array types are equivalent.

This rule prevents writes through a widened mutable array reference from violating the original element type.

Language version 1.1 permits an array of `S` to be observed as `T[]$` when `S` is representation-safe for reads as `T`. The relation permits equivalent types, non-null values to `unknown`, nullable lifting, `int` or `float` to `number`, structured objects to `object`, and recursively compatible read-only array views. It does not permit representation-changing conversions such as `int` to `float`.

Both `S[]` and `S[]$` may convert implicitly to a compatible `T[]$`. A read-only array is never implicitly assignable to a mutable array.

Common array types preserve a mutable type only for equivalent mutable arrays. Compatible mutable and read-only operands otherwise use the least compatible read-only array view.

> For example, both assignments are valid:
>
> ```text
> var mutable: int[] = [1, 2];
> var view: number[]$ = mutable;
> ```
>
> This widening is invalid because mutable arrays are invariant:
>
> ```text
> var mutable: int[] = [1, 2];
> var widened: number[] = mutable;
> ```

## 5.8. Explicit checked casts

An explicit checked cast uses the infix `as` operator followed by a non-void type.

A checked cast validates value conformance when the source type does not already prove success and never changes the value or its representation. When static assignability proves conformance, the cast MUST succeed.

A cast is statically permitted when the source and target types can describe the same runtime value. This includes removal of nullability, refinement from `unknown`, concrete-kind checks from `number`, structural object checks, and array shape or capability checks.

Representation-changing operations are not checked casts. In particular, the implicit conversion from `int` to `float` and the contextual conversion to `string` cannot be requested with `as`.

A failed checked cast produces a runtime error attributed to the `as` expression.

For every statically permitted `value as Type`, and excluding operational failures, `value is Type` evaluates to `true` if and only if the corresponding `as` expression completes successfully when applied to the same unchanged runtime value.

Conformance of cyclic object and array graphs MUST terminate. A value revisited while it is already being checked against the same or a broader target requirement is considered conforming for that recursive edge.

An `as` expression has the target type. It does not change the static type of the original variable or expression elsewhere in the program.

The `as` operator is left-associative.

Array checked casts are shape-based. A cast to `T[]` requires write capability and current recursive conformance of every element to `T`. A cast to `T[]$` requires read capability and the same element conformance. A successful cast preserves identity and does not establish a permanent invariant against later mutation through another alias.

> For example, both expressions evaluate to `true`:
>
> ```text
> (1 as unknown) is int
> ```
>
> ```text
> $[1, 2] is int[]$
> ```
>
> This expression produces a failed-cast runtime error because the read-only literal does not provide write capability:
>
> ```text
> $[1, 2] as int[]
> ```
