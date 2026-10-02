# 4. Types

## 4.1. General rules

Types are used for compile-time validation. Runtime values also retain sufficient type and capability information to validate operations that cannot be decided statically.

Except for `void`, every denotable non-null type can be made nullable by appending `?`.

Nullable annotation is explicit. A type without `?` does not accept `null`.

Repeated nullable annotation is invalid.

> This program distinguishes a nullable element type from a nullable array type:
>
> ```text
> var nullableElements: int?[] = [1, null];
> var nullableArray: int[]? = null;
> ```

## 4.2. Primitive types

The primitive types are:

| Type | Meaning |
|---|---|
| `bool` | Boolean value |
| `int` | Signed 64-bit integer |
| `float` | IEEE 754 binary64 floating-point value |
| `number` | Numeric value represented as either `int` or `float` |
| `string` | Unicode string |
| `primitive` | Boolean, numeric, or string value |

Primitive values are immutable.

`number` is a non-null common supertype of `int` and `float`. It has no dedicated value representation or literal syntax. Every `number` value retains its concrete `int` or `float` representation.

Type conformance observes that concrete representation and does not apply numeric promotion. An `int` value therefore conforms to `int` and `number`, but not to `float`.

Language version 1.2 adds `primitive` as the non-null common supertype of
`bool`, `number`, and `string`. `int` and `float` are therefore transitively
assignable to `primitive` through `number`.

`primitive` has no dedicated runtime representation or literal syntax. A value
stored as `primitive` retains its concrete `bool`, `int`, `float`, or `string`
representation. It is narrower than `unknown`: objects and arrays are not
primitive values.

> For example, the initializer of `total` converts the `int` operand to `float`, while the value stored in `count` retains its concrete `int` representation:
>
> ```text
> var total: float = 1 + 2.5;
> var count: number = 3;
> ```
>
> A mixed primitive array can infer `primitive[]` in language version 1.2:
>
> ```text
> var values = [true, 1, 2.5, "text"];
> ```
>
> The abstract type does not enable dynamic arithmetic:
>
> ```text
> var value: primitive = 1;
> return value + 1;
> ```
>
> The preceding program is invalid because arithmetic requires a statically
> numeric operand.

## 4.3. The `unknown` type

`unknown` is the top type for non-null values.

Every non-null value is assignable to `unknown`. A nullable value is not assignable to `unknown` unless its nullability is removed by an explicit checked cast at runtime.

`unknown?` is the top type for all values, including `null`.

A value whose static type is `unknown` cannot be used by an operation requiring a more specific type without an explicit checked cast.

Equality, identity comparison, assignment to another compatible location, argument passing to an `unknown` parameter, and return as `unknown` remain valid.

> For example, refinement from `unknown` requires a checked cast:
>
> ```text
> var value: unknown = 42;
> var result: int = value as int;
> return result;
> ```
>
> This declaration is invalid because `unknown` is not implicitly assignable to `int`:
>
> ```text
> var value: unknown = 42;
> var result: int = value;
> ```

## 4.4. The `object` type

`object` is the generic open object type.

It represents a non-null object with dynamically named properties whose values have type `unknown?`.

The dynamic operations exposed by `object` depend on the open-objects profile option. The property-existence setting exposes only `has`, while the enabled setting exposes all dynamic operations, as defined in [Section 14.4](14-language-profiles.md#144-open-objects).

Arrays and primitive values are not objects.

`object?` additionally accepts `null`.

> For example, an open object literal is assignable to `object`:
>
> ```text
> var item: object = @{ name: "MuLang" };
> return item has "name";
> ```

## 4.5. Structured object types

Structured object types are declared by the host environment and cannot be declared in source code.

A structured object type defines:

- a stable type name;
- a set of known properties;
- the type of each known property;
- whether each known property is required or optional;
- whether the object is closed or open.

Structured objects are closed by default.

A closed object rejects access to, assignment to, and removal of properties not declared by its schema.

An open structured object retains its known properties and permits additional dynamic properties.

Open structured objects require open objects to be enabled, as defined in [Section 14.4](14-language-profiles.md#144-open-objects).

Additional properties always have type `unknown?`.

An optional property and a nullable property are distinct:

- an optional property may be absent;
- a nullable property may be present with the value `null`.

A known property remains a known property even when optional.

## 4.6. Array types

A mutable array type is written as an element type followed by `[]`.

Language version 1.1 adds read-only array views, written with `$` immediately after the array suffix: `T[]$`.

The `$` modifier removes write capability from the immediately preceding array construction. It is shallow: contained objects and arrays retain the capabilities expressed by their own types.

The canonical suffix order is the element type, `[]`, optional `$`, then optional `?`. Consequently, element nullability and array nullability remain independent. Repeated `$` modifiers and `$` following array nullability are invalid.

Arrays are homogeneous. Every element MUST be assignable to the declared element type.

The generic array type is `unknown[]`.

The generic read-only array type is `unknown[]$`.

In language version 1.2, a read-only array view may widen a concrete primitive
element type to `primitive`. Mutable arrays remain invariant.

Nullability binds to the immediately preceding type construction. Element nullability and array nullability are independent.

Empty array literals require an expected array type or an explicit type context.

Every array type implicitly defines a read-only intrinsic property named `length` with type `int`.

`length` is not a reserved keyword, is not part of a host-provided object schema, and cannot be overridden by the host environment.

Mutable arrays and their read-only views preserve the same logical identity and storage. A read-only view observes mutations performed through another mutable alias.

> For example, mutation through the mutable alias is visible through the read-only view:
>
> ```text
> var mutable = [1, 2];
> var view: number[]$ = mutable;
> mutable[0] = 3;
> return view[0];
> ```
>
> This assignment is invalid because a read-only array is not an assignment target:
>
> ```text
> var values: int[]$ = $[1];
> values[0] = 2;
> ```

## 4.7. The `void` type

`void` is permitted only as:

- the return type of a host-provided function;
- the return type of a user-defined function;
- the declared result of a program compilation.

`void` is not a value type, cannot be nullable, and cannot be used for variables, properties, array elements, or function parameters.

> For example, `void` is valid as the return type of this function:
>
> ```text
> func doNothing(): void {
>     return;
> }
> doNothing();
> ```
