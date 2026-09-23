# 12. Statements

## 12.1. Statement categories

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

## 12.2. Semicolons

Semicolons are mandatory for simple statements.

Blocks and control-flow statements do not require a trailing semicolon.

## 12.3. Blocks

A block contains zero or more statements enclosed in braces.

A block establishes a lexical scope.

## 12.4. Variable declarations

A variable declaration introduces one local variable.

Multiple declarators in one statement are not supported.

The variable is not in scope within its own initializer.

A declaration without an initializer MUST have an explicit type annotation and is subject to definite-assignment analysis.

A local variable declaration cannot be used directly as the embedded statement of an `if`, `while`, or `for`. It MUST be enclosed in a block.

## 12.5. Assignment statements

Assignment is a statement and does not produce a value.

Valid assignment targets are:

- a mutable local variable;
- a known or dynamic object property;
- an array element.

The availability of object-property and array-element assignment depends on the language profile as defined in [Section 18.6](18-language-profiles.md#186-mutations). Dynamic object-property assignment additionally depends on [Section 18.5](18-language-profiles.md#185-open-objects).

The intrinsic array `length` property is not an assignment target.

An element accessed through a read-only array type is not an assignment target, independently of the language profile's mutable-array mutation setting.

Global bindings cannot be assigned.

The assigned value MUST be statically assignable to the target type.

The runtime adapter MAY reject property or element mutation even when it is statically valid. Such rejection produces a runtime error.

Compound assignments are not included in language version 1.

## 12.6. Property removal

Property removal uses postfix `~` followed by a semicolon.

The availability of property removal depends on the language profile as defined in [Section 18.6](18-language-profiles.md#186-mutations). Removal through the additional-property space of an open object additionally depends on [Section 18.5](18-language-profiles.md#185-open-objects).

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

## 12.7. Call statements

A call statement consists of a function call followed by a semicolon.

The return value of a non-void function is discarded.

No other expression can be used as a statement.

## 12.8. Conditional statements

An `if` condition MUST satisfy the selected condition semantics in [Section 18.9](18-language-profiles.md#189-conditions).

The `else` branch is optional and associates with the nearest unmatched `if`.

## 12.9. While statements

A `while` condition MUST satisfy the selected condition semantics in [Section 18.9](18-language-profiles.md#189-conditions).

The availability of `while` statements depends on the language profile as defined in [Section 18.3](18-language-profiles.md#183-loops-and-loop-control).

The condition is evaluated before every iteration.

## 12.10. For statements

The `for` statement uses C#-style initializer, condition, and iterator clauses.

The availability of `for` statements depends on the language profile as defined in [Section 18.3](18-language-profiles.md#183-loops-and-loop-control).

The initializer MAY be:

- absent;
- one local variable declaration;
- one assignment statement without its terminating semicolon;
- one function call without its terminating semicolon.

The condition MAY be absent. An absent condition is equivalent to `true`.

A present condition MUST satisfy the selected condition semantics in [Section 18.9](18-language-profiles.md#189-conditions).

The iterator MAY be:

- absent;
- one assignment statement without its terminating semicolon;
- one function call without its terminating semicolon.

The `for` initializer establishes a scope containing the condition, iterator, and body.

Multiple comma-separated initializers or iterators are not supported.

## 12.11. Break and continue

`break` and `continue` are valid only within `while` or `for`.

Each statement MAY include one positive integer literal indicating the number of enclosing loops affected. The literal defaults to `1` when omitted and MUST NOT exceed the number of loops enclosing the statement.

The availability of explicit loop-control levels depends on the language profile as defined in [Section 18.3](18-language-profiles.md#183-loops-and-loop-control).

`break` terminates the selected enclosing loop.

`continue` begins the next iteration of the selected enclosing loop.

When the selected loop is a `for` statement, `continue` transfers control to that loop's iterator before reevaluating its condition.

## 12.12. Return

`return` exits the current user-defined function or the top-level program.

A non-void function or program requires a return expression assignable to its declared result type.

A void function or program permits only `return` without an expression.

Every reachable path of a non-void function or program MUST return a value.

`break` and `continue` cannot cross a function boundary.
