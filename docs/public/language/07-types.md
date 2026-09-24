# 7. Types

## 7.1. General rules

Types are used for compile-time validation. Runtime values also retain sufficient type and capability information to validate operations that cannot be decided statically.

Except for `void`, every denotable non-null type can be made nullable by appending `?`.

Nullable annotation is explicit. A type without `?` does not accept `null`.

Repeated nullable annotation is invalid.

## 7.2. Primitive types

The primitive types are:

| Type | Meaning |
|---|---|
| `bool` | Boolean value |
| `int` | Signed 64-bit integer |
| `float` | IEEE 754 binary64 floating-point value |
| `number` | Generic numeric value whose concrete runtime kind is `int` or `float` |
| `string` | Unicode string |

Primitive values are immutable.

`number` is a non-null common supertype of `int` and `float`. It has no dedicated runtime representation and no dedicated literal syntax. Every runtime `number` value retains its concrete `int` or `float` representation.

Runtime type conformance observes that concrete representation and does not apply numeric promotion. An `int` value therefore conforms to `int` and `number`, but not to `float`.

## 7.3. The `unknown` type

`unknown` is the top type for non-null values.

Every non-null value is assignable to `unknown`. A nullable value is not assignable to `unknown` unless its nullability is removed by an explicit checked cast at runtime.

`unknown?` is the top type for all values, including `null`.

A value whose static type is `unknown` cannot be used by an operation requiring a more specific type without an explicit checked cast.

Equality, identity comparison, assignment to another compatible location, argument passing to an `unknown` parameter, and return as `unknown` remain valid.

## 7.4. The `object` type

`object` is the generic open object type.

It represents a non-null object with dynamically named properties whose values have type `unknown?`.

The dynamic operations exposed by `object` depend on the open-objects profile option. `PropertyExistenceOnly` exposes only `has`, while `Enabled` exposes all dynamic operations, as defined in [Section 18.5](18-language-profiles.md#185-open-objects).

Arrays and primitive values are not objects.

`object?` additionally accepts `null`.

## 7.5. Structured object types

Structured object types are declared by the provider and cannot be declared in source code.

A structured object type defines:

- a stable type name;
- a set of known properties;
- the type of each known property;
- whether each known property is required or optional;
- whether the object is closed or open.

Structured objects are closed by default.

A closed object rejects access to, assignment to, and removal of properties not declared by its schema.

An open structured object retains its known properties and permits additional dynamic properties.

Open structured objects require `OpenObjects.Enabled`, as defined in [Section 18.5](18-language-profiles.md#185-open-objects).

Additional properties always have type `unknown?`.

An optional property and a nullable property are distinct:

- an optional property may be absent;
- a nullable property may be present with the value `null`.

A known property remains a known property even when optional.

## 7.6. Array types

A mutable array type is written as an element type followed by `[]`.

Language version 2 adds read-only array views, written with `$` immediately after the array suffix: `T[]$`.

The `$` modifier removes write capability from the immediately preceding array construction. It is shallow: contained objects and arrays retain the capabilities expressed by their own types.

The canonical suffix order is the element type, `[]`, optional `$`, then optional `?`. Consequently, element nullability and array nullability remain independent. Repeated `$` modifiers and `$` following array nullability are invalid.

Arrays are homogeneous. Every element MUST be assignable to the declared element type.

The generic array type is `unknown[]`.

The generic read-only array type is `unknown[]$`.

Nullability binds to the immediately preceding type construction. Element nullability and array nullability are independent.

Empty array literals require an expected array type or an explicit type context.

Every array type implicitly defines a read-only intrinsic property named `length` with type `int`.

`length` is not a reserved keyword, is not part of a provider schema, and cannot be overridden by the provider.

Mutable arrays and their read-only views preserve the same logical identity and storage. A read-only view observes mutations performed through another mutable alias.

## 7.7. The `void` type

`void` is permitted only as:

- the return type of a provider function;
- the return type of a user-defined function;
- the declared result of a program compilation.

`void` is not a value type, cannot be nullable, and cannot be used for variables, properties, array elements, or function parameters.

## 7.8. Internal types

The compiler MAY use non-denotable internal types for:

- the `null` literal;
- erroneous expressions;
- unresolved constructs.

These internal types MUST NOT be exposed as provider-defined or source-denotable types.
