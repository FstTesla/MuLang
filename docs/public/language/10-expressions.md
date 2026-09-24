# 10. Expressions

## 10.1. Expression categories

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

## 10.2. Evaluation order

Expressions are evaluated from left to right.

Function arguments are evaluated from left to right before invocation.

Property and element assignment evaluate:

1. the target;
2. the property key or array index, when computed;
3. the assigned value;
4. the mutation.

Each operand MUST be evaluated exactly once.

## 10.3. Array literals

An array literal contains zero or more comma-separated expressions enclosed in square brackets.

Language version 2 also provides read-only array literals opened by the single `$[` token and closed by `]`.

A non-empty array literal MAY contain one trailing comma after its final expression.

The availability of trailing commas depends on the language profile as defined in [Section 18.7](18-language-profiles.md#187-trailing-commas).

All elements MUST have a common type under the implicit conversion rules. Each element is converted to that common type.

Numeric elements use the numeric conversion hierarchy to determine their common type: `int` with `float` produces `float`, while a static `number` element produces `number`.

A mixture of `T` and the `null` literal has `T?` as its common type when all non-null elements have a common non-null type `T`.

An empty array literal requires an expected array type from its context.

Normal array literals create mutable arrays. Read-only array literals create values exposing only read capability.

A read-only literal cannot be contextually converted to a mutable array. A normal literal may be contextually converted to a compatible read-only view without copying.

## 10.4. Object literals

A closed object literal contains zero or more comma-separated property initializers enclosed in `{` and `}`.

An open object literal contains the same contents enclosed in `@{` and `}`.

The availability of open object literals depends on the language profile as defined in [Section 18.5](18-language-profiles.md#185-open-objects).

A non-empty object literal MAY contain one trailing comma after its final property initializer.

The availability of trailing commas depends on the language profile as defined in [Section 18.7](18-language-profiles.md#187-trailing-commas).

Each property initializer consists of an identifier or string literal property name, either the `:` token or the optional-property `?:` token, and a required expression.

The `?:` token declares the property optional in the anonymous structured type inferred for an object literal. It is a single lexical token: whitespace is not permitted between `?` and `:`. It does not make the initializer expression optional: the property is always present in the newly created object.

When an object literal is contextually typed by a provider-declared structured type, the expected type determines property optionality and the literal marker does not alter it.

Computed property names and property spread are not supported.

Duplicate property names are a compile-time error.

An object literal has an inferred anonymous structured type whose known required properties correspond to its initializers.

A closed object literal has a closed inferred type.

An open object literal has an open inferred type and permits additional properties of type `unknown?`.

Object literals create mutable objects.

## 10.5. Property access

Dot access requires a statically known property name.

Element-style property access accepts a string expression as the property key.

Arrays support dot access only for the intrinsic `length` property.

Access to a known property has the type declared by the structured object schema.

Access to a dynamic property of an open object has type `unknown?`.

The availability of dynamic property access depends on the language profile as defined in [Section 18.5](18-language-profiles.md#185-open-objects).

A dynamic property may be present with the value `null`. Property absence remains distinct from a present null value and can be tested with `has`.

Access to an absent optional or dynamic property produces a runtime error unless optional access is used.

Access to a member through a nullable value is statically permitted but produces a runtime error when the target is `null`, unless optional access is used.

## 10.6. Optional access

Optional property access uses `target?.property`.

Optional element access uses `target?.[index]`.

Optional access is always available and is not controlled by the language profile.

When the target is `null`, optional access:

- does not evaluate the property or index operation;
- produces `null`;
- has a nullable result type.

When a dynamically named property is absent, optional access produces `null`.

Optional access to a known required property only affects a nullable target; it does not change the property schema.

## 10.7. Array access

Array indexes have type `int`.

An index outside the valid array range produces a runtime error.

Array element access through a nullable array is statically permitted and produces a runtime error when the array is `null`, unless optional access is used.

The intrinsic `length` property returns the current number of elements as a non-negative `int`.

Access to `length` through a nullable array is statically permitted and produces a runtime error when the array is `null`. Optional access through `array?.length` produces `null` for a null array and otherwise returns its length, giving the expression type `int?`.

Array element syntax cannot be used to access `length`; array indexes always require `int`.

The intrinsic `length` property is not considered an object property and is not visible to the `has` operator.

Every array element read validates the retrieved runtime value against the statically expected element type. A mutation through another alias that invalidates the current shape therefore causes a runtime type error on the later read.

## 10.8. Function calls

Functions may be declared by the provider or by leading top-level `func` declarations in program mode.

The declaration and invocation of user-defined functions are controlled as defined in [Section 18.2](18-language-profiles.md#182-user-defined-functions-and-recursion). Calls to provider functions are independently controlled as defined in [Section 18.4](18-language-profiles.md#184-provider-calls).

Function names are resolved only in call position.

Functions are synchronous, cannot be overloaded, and require exactly the declared number of arguments.

User-defined functions require explicit parameter and return types. They may return `void`, support forward calls and recursion, and are not first-class values.

The availability of recursion depends on the language profile as defined in [Section 18.2](18-language-profiles.md#182-user-defined-functions-and-recursion).

User-defined function names MUST NOT conflict with provider function names. User-defined functions cannot be nested.

Arguments MUST be assignable to their corresponding parameter types.

A void-returning call can only be used as a call statement.

A non-void call MAY be used as an expression or discarded as a call statement.

The compiler MUST assume that every function call can have observable side effects.

## 10.9. Nullable operands

The compiler does not perform flow-sensitive narrowing.

Operations whose operand type is nullable are statically permitted when the corresponding non-null type supports the operation.

The operation MUST validate the operand at runtime and produce a MuLang runtime error when a required operand is `null`.

Comparisons explicitly defined for `null` do not require non-null operands.

Null coalescing is explicitly defined for nullable operands in [Section 11.8](11-operators.md#118-null-coalescing-operator).

## 10.10. Conditional expression

The conditional expression follows C# precedence and right associativity.

Its condition MUST satisfy the selected condition semantics in [Section 18.9](18-language-profiles.md#189-conditions).

Its branches MUST have a common type according to the language conversion rules.

Only the selected branch is evaluated.

## 10.11. Compile-time constant evaluation

When compile-time constant folding is enabled by the selected language profile, the compiler MUST simplify constant primitive expressions after successful binding and before lowering, using the same primitive semantics as runtime evaluation.

A constant primitive expression is a primitive literal or an expression whose required operands have been reduced to primitive literals. Compile-time evaluation applies to:

- intrinsic unary and binary primitive operators;
- primitive value conversions and checked casts;
- primitive type tests and truthiness normalization;
- null coalescing;
- conditional expressions;
- conditional Boolean operators.

Compile-time evaluation MUST preserve source evaluation order and short-circuit behavior. An operand or conditional branch that is known not to be evaluated MUST NOT be evaluated for constant folding and MUST NOT produce a constant-evaluation diagnostic.

Provider calls, user-defined calls, global or local reads, array and object creation, member access, and element access are not constant expressions. Constant subexpressions within them MAY still be simplified.

If evaluating a required constant subexpression produces integer overflow, integer division or remainder by zero, an invalid shift count, or a failed checked cast, compilation MUST report an error and MUST NOT produce an executable artifact.

The compiler MUST NOT materialize mutable arrays or objects as shared compile-time constants.

When compile-time constant folding is disabled, the compiler MUST preserve these expressions for runtime evaluation and MUST NOT produce constant-evaluation diagnostics.
