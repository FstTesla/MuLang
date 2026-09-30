# 10. Statements

## 10.1. Statement categories

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

An explicit empty statement produces a redundant-empty-statement warning unless it is used directly as the body of an `if`, `else`, `while`, or `for` statement. Empty statements synthesized during syntax-error recovery do not produce this warning.

## 10.2. Semicolons

Semicolons are mandatory for simple statements.

Blocks and control-flow statements do not require a trailing semicolon.

> ```text
> var value = 1;
> if (value > 0) {
>     value = value + 1;
> }
> ```

## 10.3. Blocks

A block contains zero or more statements enclosed in braces.

A block establishes a lexical scope.

> ```text
> {
>     var temporary = 1;
> }
> ```

## 10.4. Variable declarations

A variable declaration introduces one local variable.

Multiple declarators in one statement are not supported.

The variable is not in scope within its own initializer.

A declaration without an initializer MUST have an explicit type annotation and is subject to definite-assignment analysis.

A declaration name followed by `$` introduces a read-only local as defined in [Section 6.1](06-variables-and-scope.md#61-local-variables).

A local variable declaration cannot be used directly as the embedded statement of an `if`, `while`, or `for`. It MUST be enclosed in a block.

> This form is valid:
>
> ```text
> if (true) {
>     var value = 1;
> }
> ```

## 10.5. Assignment statements

Assignment is a statement and does not produce a value.

Valid assignment targets are:

- a mutable local variable;
- a read-only local variable that is not already assigned on any incoming control-flow path and whose assignment cannot reach a loop back-edge that retains the same variable instance;
- a known or dynamic object property;
- an array element.

The availability of object-property and array-element assignment depends on the language profile as defined in [Section 14.5](14-language-profiles.md#145-mutations). Dynamic object-property assignment additionally depends on [Section 14.4](14-language-profiles.md#144-open-objects).

The intrinsic array `length` property is not an assignment target.

An element accessed through a read-only array type is not an assignment target, independently of the language profile's mutable-array mutation setting.

Global bindings cannot be assigned.

The assigned value MUST be statically assignable to the target type.

A host-provided object or array MAY reject property or element mutation even when it is statically valid. Such rejection produces a runtime error.

Compound assignments are not included in language version 1.

> For example, array element assignment is a statement:
>
> ```text
> var values = [1, 2];
> values[0] = 3;
> ```
>
> The expression form `values[0] = 3` cannot be nested inside another expression.

## 10.6. Property removal

Property removal uses postfix `~` followed by a semicolon.

The availability of property removal depends on the language profile as defined in [Section 14.5](14-language-profiles.md#145-mutations). Removal through the additional-property space of an open object additionally depends on [Section 14.4](14-language-profiles.md#144-open-objects).

The operand MUST be a property access or string-keyed element access.

The selected property MUST be statically known to be removable because:

- it belongs to the additional-property space of an open structured object; or
- it belongs to an `object` value; or
- it is a known optional property.

A known required property cannot be removed.

Array elements cannot be removed with this statement.

The intrinsic array `length` property cannot be removed.

Removal of an absent property produces a runtime error.

A host-provided object MAY reject removal from an otherwise valid dynamic property target. Such rejection produces a runtime error.

Property removal has no value and cannot occur inside an expression.

> For example, this program removes an additional property and returns `false`:
>
> ```text
> var item = @{ value: 1 };
> item.value~;
> return item has "value";
> ```

## 10.7. Call statements

A call statement consists of a function call followed by a semicolon.

The return value of a non-void function is discarded.

No other expression can be used as a statement.

> Given a host function `log(int): void`, this is a valid call statement:
>
> ```text
> log(42);
> ```

## 10.8. Conditional statements

An `if` condition MUST satisfy the selected condition semantics in [Section 14.8](14-language-profiles.md#148-conditions).

The `else` branch is optional and associates with the nearest unmatched `if`.

> Given a host global `condition: bool`:
>
> ```text
> if (condition) {
>     return 1;
> } else {
>     return 0;
> }
> ```

## 10.9. While statements

A `while` condition MUST satisfy the selected condition semantics in [Section 14.8](14-language-profiles.md#148-conditions).

The availability of `while` statements depends on the language profile as defined in [Section 14.2](14-language-profiles.md#142-loops-and-loop-control).

The condition is evaluated before every iteration.

> ```text
> var value = 0;
> while (value < 3) {
>     value = value + 1;
> }
> return value;
> ```

## 10.10. For statements

The `for` statement contains an initializer clause, a condition clause, and an iterator clause, separated by semicolons and enclosed in parentheses.

The availability of `for` statements depends on the language profile as defined in [Section 14.2](14-language-profiles.md#142-loops-and-loop-control).

The initializer MAY be:

- absent;
- one local variable declaration;
- one assignment statement without its terminating semicolon;
- one function call without its terminating semicolon.

The condition MAY be absent. An absent condition is equivalent to `true`.

A present condition MUST satisfy the selected condition semantics in [Section 14.8](14-language-profiles.md#148-conditions).

The iterator MAY be:

- absent;
- one assignment statement without its terminating semicolon;
- one function call without its terminating semicolon.

The `for` initializer establishes a scope containing the condition, iterator, and body.

Multiple comma-separated initializers or iterators are not supported.

> ```text
> var total = 0;
> for (var index = 0; index < 3; index = index + 1) {
>     total = total + index;
> }
> return total;
> ```

## 10.11. Break and continue

`break` and `continue` are valid only within `while` or `for`.

Each statement MAY include one positive integer literal indicating the number of enclosing loops affected. The literal defaults to `1` when omitted and MUST NOT exceed the number of loops enclosing the statement.

The availability of explicit loop-control levels depends on the language profile as defined in [Section 14.2](14-language-profiles.md#142-loops-and-loop-control).

`break` terminates the selected enclosing loop.

`continue` begins the next iteration of the selected enclosing loop.

When the selected loop is a `for` statement, `continue` transfers control to that loop's iterator before reevaluating its condition.

> This program uses an explicit level to leave both loops:
>
> ```text
> while (true) {
>     while (true) {
>         break 2;
>     }
> }
> return 0;
> ```

## 10.12. Return

`return` exits the current user-defined function or the top-level program.

User-defined function return contracts are specified in [Section 7.5](07-user-defined-functions.md#75-returns-and-control-flow).

A non-void function or program requires a return expression assignable to its declared result type.

A void function or program permits only `return` without an expression.

Every reachable path of a non-void function or program MUST return a value.

`break` and `continue` cannot cross a function boundary.

> Given a host global `condition: bool`, both reachable paths in this program return a value:
>
> ```text
> if (condition) {
>     return 1;
> }
> return 0;
> ```
