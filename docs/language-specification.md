# MuLang Language Specification

## Status

This document is a draft specification for the first version of MuLang. It is intended to define the language independently of its .NET implementation.

The key words **MUST**, **MUST NOT**, **REQUIRED**, **SHOULD**, **SHOULD NOT**, and **MAY** are to be interpreted as normative requirements.

## 1. Design goals

MuLang is a small, embeddable language for validating and executing expressions and imperative programs supplied as text.

The language is designed to:

- support expression-only and statement-based compilation;
- use a small, explicitly defined type system;
- obtain all global variables, functions, and structured types from a provider;
- prevent implicit access to runtime-specific APIs;
- produce deterministic diagnostics before execution whenever possible;
- preserve the same language semantics across future runtime exporters;
- allow controlled mutation of provider-supplied objects and arrays;
- remain suitable for execution with cancellation and resource limits.

MuLang uses C# as its primary reference for lexical conventions, expressions, statements, operator precedence, and control flow. Differences from C# are defined by this specification and are intentional.

## 2. Non-goals

The first language version does not provide:

- user-defined structured types;
- classes, inheritance, interfaces, or generics;
- function values or delegates;
- asynchronous functions;
- function overloads;
- optional or variadic function parameters;
- tuples;
- heterogeneous arrays;
- general union types;
- exception handling in source code;
- reflection or implicit access to runtime members;
- a standard library;
- comments;
- flow-sensitive type narrowing;
- assignment expressions.

## 3. Compilation modes

Every compilation MUST explicitly select one of the following modes.

### 3.1. Expression mode

Expression mode accepts exactly one expression followed by the end of the source text.

The expression value is the result of execution. A void-returning function call is not valid as the root of expression mode.

Statements, including `return`, are not valid in expression mode.

### 3.2. Program mode

Program mode accepts a sequence of statements followed by the end of the source text.

Program mode MAY begin with zero or more top-level function declarations. All function declarations MUST precede executable statements.

The availability of user-defined function declarations and recursion depends on the language profile as defined in Section 18.2.

A pure expression is not a statement. A function call is the only expression permitted as an expression statement.

A non-void program MUST return a value on every reachable path. A void program MAY complete without an explicit `return`.

## 4. Source text

Source text is a sequence of Unicode scalar values.

Implementations MUST track source spans as zero-based offsets into the original source text. Diagnostics SHOULD additionally expose one-based line and column positions.

The language is case-sensitive.

## 5. Lexical structure

### 5.1. Whitespace

Whitespace separates tokens and otherwise has no semantic meaning.

Whitespace consists of spaces, horizontal tabs, carriage returns, and line feeds.

### 5.2. Comments

Comments are not supported. Character sequences commonly used to introduce comments MUST be tokenized according to the normal operator and punctuation rules or reported as invalid tokens.

### 5.3. Identifiers

An identifier starts with a Unicode letter or underscore and continues with Unicode letters, decimal digits, or underscores.

Identifiers are case-sensitive.

An identifier that exactly matches a reserved keyword cannot be used as an identifier.

### 5.4. Reserved keywords

The first language version reserves:

- `as`
- `bool`
- `break`
- `continue`
- `else`
- `false`
- `float`
- `func`
- `for`
- `has`
- `if`
- `int`
- `is`
- `null`
- `number`
- `object`
- `return`
- `string`
- `true`
- `unknown`
- `var`
- `void`
- `while`

Provider-defined function, global, and type names MUST NOT use reserved keywords.

### 5.5. Boolean literals

The boolean literals are `true` and `false`.

### 5.6. Null literal

The null literal is `null`.

`null` is a value but is not a denotable type.

### 5.7. Integer literals

An integer literal consists of an optional leading `+` or `-` sign followed by a non-empty sequence of decimal digits without a decimal separator or exponent.

The lexer emits the sign and unsigned numeric portion as separate tokens. The parser combines them into one signed literal syntax node when a sign is followed by a numeric token in a position where a literal can occur.

Whitespace between the sign and numeric token has no semantic meaning.

The complete signed value MUST be representable as a signed 64-bit integer. A value outside that range is a compile-time error.

### 5.8. Float literals

A float literal consists of an optional leading `+` or `-` sign and a numeric portion containing a decimal separator, an exponent, or both.

Float literals use `.` as the decimal separator and are independent of host culture.

Float values use IEEE 754 binary64 semantics. The source syntax does not provide literals for NaN or infinity.

The lexer emits the sign and unsigned numeric portion as separate tokens. The parser combines them into one signed literal syntax node under the same rules as integer literals.

### 5.9. String literals

String literals are delimited by double quotes.

The supported escape sequences are:

- `\"` for a double quote;
- `\\` for a backslash;
- `\n` for a line feed;
- `\r` for a carriage return;
- `\t` for a horizontal tab;
- `\0` for the null character;
- `\uXXXX` for a four-hex-digit Unicode code unit;
- `\UXXXXXXXX` for an eight-hex-digit Unicode scalar value.

Unicode escapes MUST NOT encode an unpaired surrogate or a value outside the Unicode scalar range.

An invalid escape sequence or unterminated string is a lexical error.

String values are sequences of Unicode scalar values and are compared ordinally.

## 6. Grammar conventions

The grammar in this document uses the following notation:

- quoted text denotes a token;
- `?` denotes an optional production;
- `*` denotes zero or more repetitions;
- `+` denotes one or more repetitions;
- `|` separates alternatives;
- parentheses group productions.

The grammar is descriptive where precedence is more precisely defined by the operator table.

## 7. Types

### 7.1. General rules

Types are used for compile-time validation. Runtime values also retain sufficient type and capability information to validate operations that cannot be decided statically.

Except for `void`, every denotable non-null type can be made nullable by appending `?`.

Nullable annotation is explicit. A type without `?` does not accept `null`.

Repeated nullable annotation is invalid.

### 7.2. Primitive types

The primitive types are:

| Type | Meaning |
|---|---|
| `bool` | Boolean value |
| `int` | Signed 64-bit integer |
| `float` | IEEE 754 binary64 floating-point value |
| `number` | Generic numeric value whose concrete runtime kind is `int` or `float` |
| `string` | Unicode string |

Primitive values are immutable.

`number` is a non-null common supertype of `int` and `float`. It has no dedicated runtime representation and no dedicated literal syntax.

### 7.3. The `unknown` type

`unknown` is the top type for non-null values.

Every non-null value is assignable to `unknown`. A nullable value is not assignable to `unknown` unless its nullability is removed by an explicit checked conversion at runtime.

`unknown?` is the top type for all values, including `null`.

A value whose static type is `unknown` cannot be used by an operation requiring a more specific type without an explicit checked conversion.

Equality, identity comparison, assignment to another compatible location, argument passing to an `unknown` parameter, and return as `unknown` remain valid.

### 7.4. The `object` type

`object` is the generic open object type.

It represents a non-null object with dynamically named properties whose values have type `unknown?`.

The dynamic operations exposed by `object` depend on the open-objects profile option as defined in Section 18.5.

Arrays and primitive values are not objects.

`object?` additionally accepts `null`.

### 7.5. Structured object types

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

The availability of open structured objects and their dynamic operations depends on the language profile as defined in Section 18.5.

Additional properties always have type `unknown?`.

An optional property and a nullable property are distinct:

- an optional property may be absent;
- a nullable property may be present with the value `null`.

A known property remains a known property even when optional.

### 7.6. Array types

An array type is written as an element type followed by `[]`.

Arrays are homogeneous. Every element MUST be assignable to the declared element type.

The generic array type is `unknown[]`.

Nullability binds to the immediately preceding type construction. Element nullability and array nullability are independent.

Empty array literals require an expected array type or an explicit type context.

Every array type implicitly defines a read-only intrinsic property named `length` with type `int`.

`length` is not a reserved keyword, is not part of a provider schema, and cannot be overridden by the provider.

### 7.7. The `void` type

`void` is permitted only as:

- the return type of a provider function;
- the return type of a user-defined function;
- the declared result of a program compilation.

`void` is not a value type, cannot be nullable, and cannot be used for variables, properties, array elements, or function parameters.

### 7.8. Internal types

The compiler MAY use non-denotable internal types for:

- the `null` literal;
- erroneous expressions;
- unresolved constructs.

These internal types MUST NOT be exposed as provider-defined or source-denotable types.

## 8. Assignability and conversions

### 8.1. Identity conversion

A value is assignable to the same type.

### 8.2. Nullable conversion

A value of type `T` is assignable to `T?`.

The `null` literal is assignable to every nullable type and to no non-null type.

A value of type `T?` is not implicitly assignable to `T`.

### 8.3. Unknown conversion

Every non-null type is implicitly assignable to `unknown`.

Every type, including nullable types, is implicitly assignable to `unknown?`.

Conversion from `unknown` or `unknown?` to a more specific type requires an explicit checked conversion.

### 8.4. Numeric conversion

`int` is implicitly convertible to `float`.

`int` and `float` are implicitly convertible to `number`.

`number` is explicitly convertible to `int` and `float`. The conversion validates the concrete runtime numeric kind and value.

An explicit conversion from `number` to `int` MUST fail at runtime when its concrete value is a `float` that is not finite, is not integral, or is outside the signed 64-bit range.

An explicit conversion from `number` to `float` converts a concrete `int` value to `float` and preserves a concrete `float` value.

There is no conversion from `float` to `int`.

Integer arithmetic is checked. Overflow produces a runtime error.

### 8.5. String conversion

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

### 8.6. Object conversion

Every non-null structured object type is assignable to `object`.

Assignment between structured object types is structural and requires compatibility of known properties, optionality, and openness.

Because structured objects are mutable, structural object types are invariant. Assignment requires the same openness and the same set of known properties, with equivalent property types and matching optionality. Type names and provider identifiers do not affect structural compatibility.

The precise structural compatibility algorithm is part of the type system and MUST NOT depend on host-runtime class inheritance.

### 8.7. Array conversion

Array types are invariant.

An array of `T` is not implicitly assignable to an array of another element type, including `unknown[]`, unless the two array types are equivalent.

This rule prevents writes through a widened mutable array reference from violating the original element type.

### 8.8. Explicit checked conversions

An explicit conversion uses the infix `as` operator followed by a non-void type.

A checked conversion performs runtime validation when static validation cannot prove success.

A failed checked conversion produces a MuLang runtime error at the `as` expression source span.

The language does not provide a general predicate that determines whether an `as` conversion will succeed. The `is` operator tests runtime assignability, which is intentionally distinct from convertibility. In particular, a value can be convertible to a type without being assignable to that type.

An `as` expression has the target type. It does not change the static type of the original variable or expression elsewhere in the program.

The `as` operator is left-associative.

## 9. Variables and scope

### 9.1. Local variables

Local variables are declared with `var`.

A declaration MUST include an explicit type annotation, an initializer, or both.

When an initializer is absent, an explicit type annotation is REQUIRED.

When a type annotation is absent, the variable type is inferred from the initializer.

The inferred type of the `null` literal alone is invalid because `null` has no denotable type.

An uninitialized local variable has no default value. Every read MUST be proven by definite-assignment analysis to occur after an assignment on every reachable control-flow path.

Local variables are mutable.

### 9.2. Scope

A program body, block, and `for` statement initializer establish lexical scopes as defined by the statement grammar.

A local variable name MUST NOT match any local or global variable name visible at the declaration point.

Local variable shadowing is not supported, including shadowing of global variables.

The permitted forms of shadowing depend on the language profile as defined in Section 18.8.

Function names and variable names occupy distinct namespaces because functions are not first-class values and can only occur in call position.

Top-level user-defined functions are visible throughout the complete program, including within functions declared earlier. Direct and mutual recursion are supported.

The availability of user-defined functions and recursion depends on the language profile as defined in Section 18.2.

Function parameters establish the root variable scope of their function body. Parameters are definitely assigned, immutable, and cannot be shadowed by local or global variables.

Function bodies cannot access locals declared by top-level executable statements or by other functions.

### 9.3. Global variables

Global variables are declared by the provider.

Global bindings are read-only from MuLang source. If a global value is an object or array, its contents MAY still be mutated through the supported property and element operations.

The availability of those mutation operations depends on the language profile as defined in Section 18.6.

## 10. Expressions

### 10.1. Expression categories

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

### 10.2. Evaluation order

Expressions are evaluated from left to right.

Function arguments are evaluated from left to right before invocation.

Property and element assignment evaluate:

1. the target;
2. the property key or array index, when computed;
3. the assigned value;
4. the mutation.

Each operand MUST be evaluated exactly once.

### 10.3. Array literals

An array literal contains zero or more comma-separated expressions enclosed in square brackets.

A non-empty array literal MAY contain one trailing comma after its final expression.

The availability of trailing commas depends on the language profile as defined in Section 18.7.

All elements MUST have a common type under the implicit conversion rules. Each element is converted to that common type.

Numeric elements use the numeric conversion hierarchy to determine their common type: `int` with `float` produces `float`, while a static `number` element produces `number`.

A mixture of `T` and the `null` literal has `T?` as its common type when all non-null elements have a common non-null type `T`.

An empty array literal requires an expected array type from its context.

Array literals create mutable arrays. A runtime adapter MAY still reject a later mutation when the value crosses a provider boundary.

### 10.4. Object literals

A closed object literal contains zero or more comma-separated property initializers enclosed in `{` and `}`.

An open object literal contains the same contents enclosed in `@{` and `}`.

The availability of open object literals depends on the language profile as defined in Section 18.5.

A non-empty object literal MAY contain one trailing comma after its final property initializer.

The availability of trailing commas depends on the language profile as defined in Section 18.7.

Each property initializer consists of an identifier or string literal property name, either the `:` token or the optional-property `?:` token, and a required expression.

The `?:` token declares the property optional in the anonymous structured type inferred for an object literal. It is a single lexical token: whitespace is not permitted between `?` and `:`. It does not make the initializer expression optional: the property is always present in the newly created object.

When an object literal is contextually typed by a provider-declared structured type, the expected type determines property optionality and the literal marker does not alter it.

Computed property names and property spread are not supported.

Duplicate property names are a compile-time error.

An object literal has an inferred anonymous structured type whose known required properties correspond to its initializers.

A closed object literal has a closed inferred type.

An open object literal has an open inferred type and permits additional properties of type `unknown?`.

Object literals create mutable objects.

### 10.5. Property access

Dot access requires a statically known property name.

Element-style property access accepts a string expression as the property key.

Arrays support dot access only for the intrinsic `length` property.

Access to a known property has the type declared by the structured object schema.

Access to a dynamic property of an open object has type `unknown?`.

The availability of dynamic property access depends on the language profile as defined in Section 18.5.

A dynamic property may be present with the value `null`. Property absence remains distinct from a present null value and can be tested with `has`.

Access to an absent optional or dynamic property produces a runtime error unless optional access is used.

Access to a member through a nullable value is statically permitted but produces a runtime error when the target is `null`, unless optional access is used.

### 10.6. Optional access

Optional property access uses `target?.property`.

Optional element access uses `target?.[index]`.

Optional access is always available and is not controlled by the language profile.

When the target is `null`, optional access:

- does not evaluate the property or index operation;
- produces `null`;
- has a nullable result type.

When a dynamically named property is absent, optional access produces `null`.

Optional access to a known required property only affects a nullable target; it does not change the property schema.

### 10.7. Array access

Array indexes have type `int`.

An index outside the valid array range produces a runtime error.

Array element access through a nullable array is statically permitted and produces a runtime error when the array is `null`, unless optional access is used.

The intrinsic `length` property returns the current number of elements as a non-negative `int`.

Access to `length` through a nullable array is statically permitted and produces a runtime error when the array is `null`. Optional access through `array?.length` produces `null` for a null array and otherwise returns its length, giving the expression type `int?`.

Array element syntax cannot be used to access `length`; array indexes always require `int`.

The intrinsic `length` property is not considered an object property and is not visible to the `has` operator.

### 10.8. Function calls

Functions may be declared by the provider or by leading top-level `func` declarations in program mode.

The declaration and invocation of user-defined functions are controlled as defined in Section 18.2. Calls to provider functions are independently controlled as defined in Section 18.4.

Function names are resolved only in call position.

Functions are synchronous, cannot be overloaded, and require exactly the declared number of arguments.

User-defined functions require explicit parameter and return types. They may return `void`, support forward calls and recursion, and are not first-class values.

The availability of recursion depends on the language profile as defined in Section 18.2.

User-defined function names MUST NOT conflict with provider function names. User-defined functions cannot be nested.

Arguments MUST be assignable to their corresponding parameter types.

A void-returning call can only be used as a call statement.

A non-void call MAY be used as an expression or discarded as a call statement.

The compiler MUST assume that every function call can have observable side effects.

### 10.9. Nullable operands

The compiler does not perform flow-sensitive narrowing.

Operations whose operand type is nullable are statically permitted when the corresponding non-null type supports the operation.

The operation MUST validate the operand at runtime and produce a MuLang runtime error when a required operand is `null`.

Comparisons explicitly defined for `null` do not require non-null operands.

Null coalescing is explicitly defined for nullable operands in Section 11.8.

### 10.10. Conditional expression

The conditional expression follows C# precedence and right associativity.

Its condition MUST satisfy the selected condition semantics in Section 18.9.

Its branches MUST have a common type according to the language conversion rules.

Only the selected branch is evaluated.

## 11. Operators

### 11.1. General rules

Operator behavior is defined by MuLang types and MUST NOT be delegated directly to host-runtime operator resolution.

No user-defined operators exist.

The selected language profile determines whether Boolean contexts use strict Boolean conditions or truthiness as defined in Section 18.9.

### 11.2. Precedence

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

### 11.3. Arithmetic operators

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

### 11.4. Relational operators

Relational operators are defined for:

- numeric operands, using numeric promotion;
- string operands, using ordinal comparison.

No ordering is defined for booleans, arrays, or objects.

### 11.5. Bitwise and eager Boolean operators

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

### 11.6. Type conversion, type checking, and property existence

`value as Type` performs the explicit checked conversion defined in Section 8.8.

`value is Type` evaluates to `true` when the runtime value is assignable to the specified non-void type under the MuLang assignability rules.

For `null`, `is` evaluates to `true` only when the tested type is nullable. The `is` operator does not narrow the operand in any subsequent expression or statement.

`target has key` checks whether an object currently contains a property. The key expression MUST have type `string`.

Dynamic property-existence tests depend on the open-objects profile option as defined in Section 18.5.

The target of `has` MUST have `object`, structured object, or a nullable form of either as its static type. A `null` target produces a runtime error.

`has` evaluates to `true` when the property is present, regardless of whether its value is `null`. It evaluates to `false` when the property is absent.

The key expression is evaluated exactly once. The `has` operator does not read the property value and does not narrow the target or property type.

### 11.7. Boolean operators

Under strict Boolean condition semantics, `!`, `&&`, and `||` require Boolean operands.

Under truthiness condition semantics, each non-void operand is normalized to `bool` according to Section 18.9 before the operator is applied.

`&&` and `||` short-circuit and evaluate operands from left to right.

`&&` and `||` always produce `bool`; they never return an operand value.

### 11.8. Null-coalescing operator

The null-coalescing expression `left ?? right` evaluates `left` exactly once.

Null coalescing is always available and is not controlled by the language profile.

The left operand MUST have a nullable type or be the `null` literal. The right operand MUST have a non-void type.

If the left operand is not `null`, its value is converted to the result type and returned without evaluating the right operand. Otherwise, the right operand is evaluated, converted to the result type, and returned.

For the `null` literal on the left, the result type is the type of the right operand.

For a left operand of type `T?`, the result type is the common type of non-null `T` and the right operand. The left operand does not by itself make the result nullable: the result is nullable only when the right operand and the common-type rules require it.

An expression whose non-null left value and right operand have no common result type is invalid.

### 11.9. Structural equality

`==` performs recursive structural equality.

`!=` is the logical negation of `==`.

For primitive values, structural equality compares values.

For `==` and `!=`, numeric operands use value equality. When an `int` is compared with a `float`, the `int` is converted to `float` before comparison. Values with static type `number` follow the same rule according to their concrete runtime kinds.

For arrays, structural equality compares lengths and corresponding elements in order.

For objects, structural equality compares the complete set of visible properties and recursively compares corresponding values. Property order is irrelevant.

Structural equality MUST safely handle cyclic object and array graphs by tracking pairs already compared.

`null` is structurally equal only to `null`.

### 11.10. Identity equality

`===` performs identity equality.

`!==` is the logical negation of `===`.

For primitive values with the same concrete type, identity equality coincides with value equality.

Numeric primitive values with different concrete types are never identical. In particular, an `int` and a `float` compare unequal with `===` and equal with `!==`, even when `==` considers their values equal.

For arrays and objects, identity is supplied by the runtime adapter and represents the same logical runtime instance.

`null` is identical only to `null`.

## 12. Statements

### 12.1. Statement categories

Program mode supports:

- block statements;
- local variable declarations;
- local assignments;
- property assignments;
- array element assignments;
- property removal;
- call statements;
- conditional statements;
- `while` statements;
- `for` statements;
- `break`;
- `continue`;
- `return`;
- empty statements.

### 12.2. Semicolons

Semicolons are mandatory for simple statements.

Blocks and control-flow statements do not require a trailing semicolon.

### 12.3. Blocks

A block contains zero or more statements enclosed in braces.

A block establishes a lexical scope.

### 12.4. Variable declarations

A variable declaration introduces one local variable.

Multiple declarators in one statement are not supported.

The variable is not in scope within its own initializer.

A declaration without an initializer MUST have an explicit type annotation and is subject to definite-assignment analysis.

A local variable declaration cannot be used directly as the embedded statement of an `if`, `while`, or `for`. It MUST be enclosed in a block.

### 12.5. Assignment statements

Assignment is a statement and does not produce a value.

Valid assignment targets are:

- a mutable local variable;
- a known or dynamic object property;
- an array element.

The availability of object-property and array-element assignment depends on the language profile as defined in Section 18.6. Dynamic object-property assignment additionally depends on Section 18.5.

The intrinsic array `length` property is not an assignment target.

Global bindings cannot be assigned.

The assigned value MUST be statically assignable to the target type.

The runtime adapter MAY reject property or element mutation even when it is statically valid. Such rejection produces a runtime error.

Compound assignments are not included in the first language version.

### 12.6. Property removal

Property removal uses postfix `~` followed by a semicolon.

The availability of property removal depends on the language profile as defined in Section 18.6. Removal through the additional-property space of an open object additionally depends on Section 18.5.

The operand MUST be a property access or string-keyed element access.

The type checker MUST prove that the selected property is removable:

- it belongs to the additional-property space of an open structured object; or
- it belongs to an `object` value; or
- it is a known optional property.

A known required property cannot be removed.

Array elements cannot be removed with this statement.

The intrinsic array `length` property cannot be removed.

Removal of an absent property produces a runtime error.

The runtime adapter MAY reject removal from an otherwise valid dynamic property target. Such rejection produces a runtime error.

Property removal has no value and cannot occur inside an expression.

### 12.7. Call statements

A call statement consists of a function call followed by a semicolon.

The return value of a non-void function is discarded.

No other expression can be used as a statement.

### 12.8. Conditional statements

An `if` condition MUST satisfy the selected condition semantics in Section 18.9.

The `else` branch is optional and associates with the nearest unmatched `if`.

### 12.9. While statements

A `while` condition MUST satisfy the selected condition semantics in Section 18.9.

The availability of `while` statements depends on the language profile as defined in Section 18.3.

The condition is evaluated before every iteration.

### 12.10. For statements

The `for` statement uses C#-style initializer, condition, and iterator clauses.

The availability of `for` statements depends on the language profile as defined in Section 18.3.

The initializer MAY be:

- absent;
- one local variable declaration;
- one assignment statement without its terminating semicolon;
- one function call without its terminating semicolon.

The condition MAY be absent. An absent condition is equivalent to `true`.

A present condition MUST satisfy the selected condition semantics in Section 18.9.

The iterator MAY be:

- absent;
- one assignment statement without its terminating semicolon;
- one function call without its terminating semicolon.

The `for` initializer establishes a scope containing the condition, iterator, and body.

Multiple comma-separated initializers or iterators are not supported.

### 12.11. Break and continue

`break` and `continue` are valid only within `while` or `for`.

Each statement MAY include one positive integer literal indicating the number of enclosing loops affected. The literal defaults to `1` when omitted and MUST NOT exceed the number of loops enclosing the statement.

The availability of explicit loop-control levels depends on the language profile as defined in Section 18.3.

`break` terminates the selected enclosing loop.

`continue` begins the next iteration of the selected enclosing loop.

When the selected loop is a `for` statement, `continue` transfers control to that loop's iterator before reevaluating its condition.

### 12.12. Return

`return` exits the current user-defined function or the top-level program.

A non-void function or program requires a return expression assignable to its declared result type.

A void function or program permits only `return` without an expression.

Every reachable path of a non-void function or program MUST return a value.

`break` and `continue` cannot cross a function boundary.

## 13. Provider environment

### 13.1. Static environment

The provider supplies an immutable static environment containing:

- global variable declarations;
- function declarations;
- structured object declarations;
- stable symbolic identifiers.

Every structured object type referenced directly or indirectly by a global, function parameter, function return type, array element, nullable type, or structured property MUST be registered in the static environment. Every reference to the same stable type identifier MUST resolve to the same type declaration.

Profile compatibility requirements for provider-declared open structured types are defined in Section 18.5.

The compiler resolves source names exclusively against local declarations and the static environment.

### 13.2. Runtime context

Execution receives a runtime context compatible with the static environment used during compilation.

The runtime context supplies:

- global values;
- function implementations;
- object and array adapters;
- cancellation;
- execution budget state.

The runtime context MUST expose no operation that was not declared by the static environment or required by the language runtime contract.

### 13.3. Environment compatibility

The static environment MUST have a deterministic fingerprint.

A compiled program MUST reject execution with a runtime context whose environment fingerprint is incompatible.

Names, types, mutability capabilities, and function signatures that affect compilation MUST contribute to compatibility.

## 14. Runtime value adapters

Runtime-specific values MUST be accessed through explicit adapters rather than implicit reflection.

The .NET runtime accepts object and array values only through its explicit object and array adapter interfaces. CLR dictionaries, lists, arrays, POCOs, and other host values are not recognized implicitly.

Global values and provider function results are recursively validated against their declared MuLang types when they cross the runtime boundary.

Deep runtime traversal is subject to the execution budget and to a configurable maximum traversal depth.

Adapters define:

- property lookup;
- property assignment;
- property removal;
- property enumeration;
- array indexing;
- array element assignment;
- array length;
- logical identity;
- conversion between runtime values and MuLang values.

Adapters do not define or customize truthiness. Determining truthiness MUST NOT enumerate properties or elements, access adapter members, or perform deep traversal.

The condition-semantics profile option and truthiness rules are defined in Section 18.9.

Static mutability is intentionally not represented in the first-version type system.

An adapter MAY reject a mutation or removal at runtime. Previous completed side effects are not rolled back.

The first language version does not provide source-level operations for inspecting whether a specific property or array element is writable or whether a property is removable. `has` reports only property presence and does not imply either capability.

## 15. Runtime errors

A runtime failure MUST be represented as a MuLang runtime error containing:

- a stable error code;
- a message;
- the source span of the operation;
- an optional runtime-specific inner exception.

Runtime errors include:

- null operand where a non-null value is required;
- failed checked conversion;
- integer overflow;
- division or remainder by integer zero;
- invalid shift count;
- invalid array index;
- absent property access;
- prohibited property or array mutation;
- prohibited property removal;
- incompatible runtime environment;
- exhausted execution budget;
- cancellation;
- provider function failure.
- exhausted user-function call depth.

Provider exceptions MUST be wrapped while preserving the original exception as the inner cause where the host runtime supports it.

The language provides no source-level mechanism for catching runtime errors.

### 15.1. Intentionally non-preventable runtime errors

The first language version intentionally permits two categories of data-dependent runtime failure that source code cannot always prevent through a prior check:

- a checked `as` conversion can fail even though no general convertibility predicate is available;
- a property assignment, array element assignment, or property removal can be rejected by the runtime adapter even though no source-level capability predicate is available.

These limitations are part of the first-version language contract rather than omissions in static validation.

Provider failures, environment incompatibility, cancellation, and budget exhaustion are operational failures controlled outside the source program and are not considered semantic check gaps.

## 16. Execution controls

Execution MUST support cancellation.

Execution MUST support a configurable budget.

The .NET runtime represents an unlimited budget with a null execution-budget value. When a budget is present, each instruction, terminator, provider call, and deeply traversed value currently has a fixed unit cost.

The budget is charged for executed portable IR instructions, provider function calls, user-defined function calls, and deep value traversal. Implementations MAY assign different fixed costs to different instruction categories, but the cost model MUST be deterministic for a given language version and profile.

Budget exhaustion produces a runtime error.

Cancellation MUST be observed at deterministic safe points, including loop back-edges, provider function boundaries, and user-defined function boundaries.

The .NET runtime MUST enforce a configurable maximum active user-function call depth. The top-level program and provider calls do not count toward this limit.

## 17. Diagnostics

Compilation diagnostics are divided into:

- lexical diagnostics;
- syntax diagnostics;
- binding diagnostics;
- type diagnostics;
- control-flow diagnostics;
- exporter diagnostics.

Every diagnostic MUST contain:

- a stable code;
- a severity;
- a source span;
- a message.

The compiler SHOULD continue after recoverable errors to report multiple independent diagnostics.

Diagnostics for syntax or capabilities disabled by the selected language profile are defined in Section 18.10.

No executable artifact may be produced when error diagnostics are present.

## 18. Language profiles

Every compilation MUST select a language profile independently from its static environment and compilation mode.

A language profile is immutable and contains:

- a language version;
- one independently typed enum value for each configurable concern;
- a deterministic language-profile fingerprint.

The standard version-one profile preserves all language syntax, capabilities, and policies described by the rest of this specification unless this section explicitly permits a restriction. It enables user-defined functions, recursion, both loop kinds, provider function calls, open objects, every mutation kind, explicit loop-control levels, and trailing commas. It uses strict Boolean conditions and prohibits variable shadowing.

The profile options and stable numeric values are:

| Property | Values | Combinable |
|---|---|---|
| `UserDefinedFunctions` | `Disabled = 0`, `Enabled = 1` | No |
| `Recursion` | `Disabled = 0`, `Enabled = 1` | No |
| `Loops` | `None = 0`, `While = 1`, `For = 2` | Yes |
| `ProviderFunctionCalls` | `Disabled = 0`, `Enabled = 1` | No |
| `OpenObjects` | `Disabled = 0`, `Enabled = 1` | No |
| `Mutations` | `None = 0`, `ObjectProperties = 1`, `ArrayElements = 2`, `PropertyRemoval = 4` | Yes |
| `MultiLevelLoopControl` | `Disabled = 0`, `Enabled = 1` | No |
| `TrailingCommas` | `Disabled = 0`, `Enabled = 1` | No |
| `ConditionSemantics` | `StrictBoolean = 0`, `Truthiness = 1` | No |
| `Shadowing` | `None = 0`, `NestedScopes = 1`, `Globals = 2` | Yes |

Both condition-semantics values are supported in version one. Profile construction MUST reject unknown enum values, unknown flag bits, and unsupported language versions. A dependent option MAY be enabled while its prerequisite is disabled; it remains dormant rather than making the profile invalid.

### 18.1. Profile fingerprint and compilation identity

The language-profile fingerprint MUST be deterministic. Its canonical input MUST include the stable label and numeric value of the language version and every profile option. It MUST NOT depend on enum declaration order, collection iteration order, process-specific hashes, or object identity.

Compilation cache identity consists of:

- source text;
- compilation mode;
- language-profile fingerprint;
- environment fingerprint.

The language-profile and environment fingerprints remain separate.

### 18.2. User-defined functions and recursion

When user-defined functions are disabled, function declarations remain recognizable for recovery but are rejected with a dedicated diagnostic. Calls resolved to a user-defined function are also rejected. Provider calls remain independently configurable.

When recursion is disabled and user-defined functions are enabled, the compiler MUST reject every strongly connected component in the user-function call graph that contains more than one function or a self-edge. Each participating function receives one useful diagnostic without diagnostics for every possible call path. Provider calls do not create recursion edges.

The recursion option is dormant when user-defined functions are disabled.

### 18.3. Loops and loop control

`Loops.While` independently permits `while`, and `Loops.For` independently permits `for`. A disabled loop body MUST still be analyzed for independent diagnostics and normal `break` and `continue` context validation.

When multi-level loop control is disabled, explicitly written levels on `break` and `continue` are rejected, including level `1`. Plain `break;` and `continue;` remain available. The option is dormant when `Loops` is `None`.

### 18.4. Provider calls

When provider function calls are disabled, a call resolved to a provider function is rejected. Provider function declarations remain valid in the environment and may be unused.

### 18.5. Open objects

The open-objects option controls:

- `@{ ... }` literals;
- dynamic member and element access;
- dynamic `has` tests;
- dynamic property assignment and removal;
- provider-declared open structured types.

When open objects are disabled, compilation rejects an environment containing any open structured type, including an unused type.

The generic `object` type remains a valid abstract supertype. With open objects disabled, it exposes no dynamic member access, element access, `has`, assignment, or removal. It remains valid for assignment, argument passing, return values, arrays and properties, equality, identity equality, `is`, and `as`.

Closed structured types remain valid. Their statically known properties remain accessible.

### 18.6. Mutations

`Mutations.ObjectProperties` independently permits assignment to an object property through member or element syntax.

`Mutations.ArrayElements` independently permits assignment to an array element.

`Mutations.PropertyRemoval` independently permits postfix `~` property-removal statements. Property removal does not depend on object-property assignment.

Mutation restrictions apply regardless of whether the value originated from a literal, global, parameter, property, array element, or function result.

### 18.7. Trailing commas

When trailing commas are disabled, a trailing comma is rejected in array, closed-object, and open-object literals. Trailing commas in argument and parameter lists remain unconditional grammar errors.

### 18.8. Shadowing

Same-scope duplicate declarations are invalid under every shadowing policy.

`Shadowing.NestedScopes` permits a declaration in a nested lexical scope to shadow an enclosing local variable or function parameter. It does not permit shadowing a global.

`Shadowing.Globals` permits locals and function parameters to shadow globals. It does not permit shadowing enclosing locals or parameters.

Combining both values permits both forms. `None` prohibits both and preserves the standard version-one policy.

### 18.9. Conditions

`ConditionSemantics.StrictBoolean` requires `if`, `while`, `for`, and conditional-expression conditions to have type `bool`. It also requires `!`, `&&`, and `||` operands to have type `bool`. Non-Boolean values produce the existing type or operator diagnostic.

`ConditionSemantics.Truthiness` accepts every non-void type in those contexts and normalizes the value to `bool` with these rules:

| Runtime value | Boolean result |
|---|---|
| `null` | `false` |
| `false` | `false` |
| `int` zero | `false` |
| `float` positive zero, negative zero, or NaN | `false` |
| empty `string` | `false` |
| every other supported value | `true` |

A value with static type `number` dispatches according to its concrete runtime `int` or `float` representation. Values with static type `unknown` or `unknown?` dispatch according to their concrete runtime value. Every non-null object and array is truthy, including empty values.

Truthiness is contextual. It does not add an implicit conversion to `bool`, a source-level Boolean cast, flow-sensitive narrowing, or changes to equality and identity. `void` remains invalid. Unsupported runtime representations produce a MuLang invalid-runtime-value error.

The standard `LanguageProfiles.Version1` profile uses `StrictBoolean`.

### 18.10. Diagnostics

Each disabled feature or independently controllable member MUST use its dedicated stable diagnostic code. Diagnostics do not carry separate feature metadata. Recognized disabled syntax SHOULD be retained sufficiently for later phases to recover and report independent diagnostics.

## 19. Portable intermediate representation

The compiler MUST lower validated source into a runtime-independent typed intermediate representation before export.

The portable IR compilation unit SHOULD contain:

- compilation mode metadata;
- the language-profile fingerprint;
- the environment fingerprint;
- a distinguished top-level entry function;
- zero or more user-defined functions;
- independent parameter, local, and temporary slot spaces for each function;

- constants;
- local slots;
- basic blocks;
- typed load and store instructions;
- global reads;
- property and array operations;
- intrinsic array-length reads;
- property removal;
- provider calls by stable symbolic identifier;
- user-defined calls by compiler-assigned stable identifier;
- explicit conversions;
- explicit null tests and branches for null-coalescing evaluation;
- truthiness normalization from one non-void source slot to one `bool` destination slot;
- branches and jumps;
- returns;
- source-span associations.

The IR MUST encode evaluation order and short-circuit behavior explicitly.

The IR validator MUST require a truthiness-normalization destination to have type `bool`, its source to have a non-void type, both slots to exist, and the source to be definitely defined.

Runtime exporters MUST NOT perform name resolution, type inference, overload resolution, or high-level control-flow interpretation.

The first language version does not require the IR to be public or serializable.

## 20. Grammar summary

This grammar describes the complete version-one syntax. A selected language profile may reject otherwise recognized function, loop, mutation, open-object, or trailing-comma constructs as defined in Sections 18.2 through 18.7.

```ebnf
expression-root
    = expression end-of-file ;

program-root
    = function-declaration* statement* end-of-file ;

function-declaration
    = "func" identifier
      "(" parameter-list? ")"
      ":" function-return-type
      block ;

parameter-list
    = parameter ("," parameter)* ;

parameter
    = identifier ":" type ;

function-return-type
    = type
    | "void" ;

statement
    = block
    | variable-declaration ";"
    | assignment ";"
    | removal ";"
    | call-expression ";"
    | if-statement
    | while-statement
    | for-statement
    | "break" integer-literal? ";"
    | "continue" integer-literal? ";"
    | return-statement ";"
    | ";" ;

block
    = "{" statement* "}" ;

variable-declaration
    = "var" identifier
      (":" type ("=" expression)?
      | "=" expression) ;

assignment
    = assignable "=" expression ;

assignable
    = identifier
    | property-access
    | element-access ;

removal
    = removable "~" ;

removable
    = property-access
    | element-access ;

if-statement
    = "if" "(" expression ")" statement
      ("else" statement)? ;

while-statement
    = "while" "(" expression ")" statement ;

for-statement
    = "for" "(" for-initializer? ";"
                expression? ";"
                for-iterator? ")"
      statement ;

for-initializer
    = variable-declaration
    | assignment
    | call-expression ;

for-iterator
    = assignment
    | call-expression ;

return-statement
    = "return" expression? ;

type
    = primary-type nullable-suffix?
      (array-suffix nullable-suffix?)* ;

primary-type
    = "bool"
    | "int"
    | "float"
    | "number"
    | "string"
    | "unknown"
    | "object"
    | provider-type-name ;

array-suffix
    = "[]" ;

nullable-suffix
    = "?" ;

array-literal
    = "[" (expression ("," expression)* ","?)? "]" ;

closed-object-literal
    = "{" property-initializer-list? "}" ;

open-object-literal
    = "@{" property-initializer-list? "}" ;

property-initializer-list
    = property-initializer ("," property-initializer)* ","? ;

property-initializer
    = (identifier | string-literal) (":" | "?:") expression ;
```

Expression grammar is defined by the precedence table rather than expanded in this summary.

## 21. Draft review decisions

The following choices are made by this draft and require explicit review before the specification is considered stable:

1. Local declarations use `var`, with optional `: Type` annotation.
2. Global bindings are read-only, while their object or array contents can be mutable.
3. Arrays are invariant because they are mutable.
4. String concatenation implicitly converts primitive and null operands when the other operand is a string.
5. Removing an absent removable property is a runtime error rather than a no-op.
6. Empty statements are supported.
7. `for` supports one initializer and one iterator operation rather than comma-separated lists.
8. Explicit checked conversions use the left-associative `as` operator and fail with a runtime error.
9. Optional property access uses `?.`, optional element access uses `?.[`, and absent dynamic properties produce `null`.
10. Prefix `~` is bitwise complement, while postfix `~` is a property-removal statement.
11. Local variables cannot shadow visible local or global variables.
12. Open object literals use `@{`, while closed object literals use `{`.
13. Arrays expose a read-only intrinsic `length` property that is not an object property.
14. Checked conversion success and runtime mutation capabilities cannot always be queried before performing the corresponding operation.
15. Null coalescing uses C# precedence and associativity, requires a nullable left operand, and derives its result from the non-null left type and the right operand.
