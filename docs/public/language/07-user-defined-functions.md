# 7. User-defined functions

User-defined functions are synchronous top-level program declarations. They are not first-class values.

## 7.1. Availability and placement

User-defined functions are available only in program mode and only when enabled by the selected language profile.

A program MAY begin with zero or more function declarations. Every function declaration MUST precede every executable top-level statement.

Functions cannot be declared in expression mode, inside another function, or inside a statement block.

When the feature is disabled, a function declaration or a call to a user-defined function is a compile-time error.

> For example, declarations precede the executable top-level statement:
>
> ```text
> func double(value: int): int {
>     return value * 2;
> }
> return double(21);
> ```

## 7.2. Declarations and signatures

A function declaration contains:

- the `func` keyword;
- one identifier;
- a parenthesized parameter list;
- an explicit return type;
- a block body.

Every parameter has a unique name within the declaration and an explicit non-void type.

The return type is an explicit value type or `void`.

Function overloading, optional parameters, variadic parameters, default arguments, generic parameters, and nested functions are not supported.

A user-defined function name MUST NOT conflict with:

- another user-defined function name;
- a host-provided function name.

Function names and variable names occupy distinct namespaces. Function names are resolved only in call position.

> Consequently, a local variable may have the same name as a function:
>
> ```text
> func value(): int {
>     return 1;
> }
> var value = value();
> return value;
> ```

## 7.3. Visibility and parameter scope

Every valid top-level function declaration is visible throughout the complete program, independently from textual declaration order. Forward calls are therefore valid.

Function parameters establish the root variable scope of the function body.

Parameters:

- are definitely assigned when the function is entered;
- are immutable;
- cannot be assignment targets.

Whether a parameter may shadow a global or be shadowed in a nested scope depends on the selected language profile.

Function bodies cannot capture locals declared by top-level executable statements or by another function. All data entering a function is supplied through parameters or host-provided globals.

Local declarations inside the body follow the ordinary block and shadowing rules.

> Forward calls are valid:
>
> ```text
> func first(): int {
>     return second();
> }
> func second(): int {
>     return 2;
> }
> return first();
> ```

## 7.4. Calls

A call supplies exactly one argument for every declared parameter, in declaration order.

Each argument MUST be statically assignable to the corresponding parameter type.

A non-void call is an expression and MAY also be used as a call statement, in which case its result is discarded.

A void call is valid only as a call statement or in another context that explicitly accepts a void operation.

Every call is potentially effectful. Its evaluation MUST NOT be omitted, duplicated, or reordered relative to other observable operations.

User-defined functions are not values: they cannot be stored in variables, passed as arguments, returned, placed in objects or arrays, or accessed without invocation syntax.

> This is therefore invalid:
>
> ```text
> func value(): int {
>     return 1;
> }
> var functionValue = value;
> ```

## 7.5. Returns and control flow

`return` exits the current function.

A non-void function requires a return expression assignable to its declared return type. Every reachable path MUST return a value.

A void function permits only `return` without an expression and MAY reach the end of its body, which is equivalent to returning without a value.

`break` and `continue` cannot cross a function boundary.

Definite-assignment analysis is performed independently for each function.

> This function is invalid because the `false` path reaches the end without returning an `int`:
>
> ```text
> func choose(flag: bool): int {
>     if (flag) {
>         return 1;
>     }
> }
> return choose(true);
> ```

## 7.6. Recursion

Direct and mutual recursion are supported when enabled by the selected language profile.

When recursion is disabled, a function MUST NOT call itself directly or participate in a cycle of calls among user-defined functions.

Calls to host-provided functions do not constitute recursion.

The recursion setting is dormant when user-defined functions are disabled.

Execution enforces the configured maximum active user-function call depth.

> For example, direct recursion can compute a factorial:
>
> ```text
> func factorial(value: int): int {
>     if (value <= 1)
>         return 1;
>     return value * factorial(value - 1);
> }
> return factorial(5);
> ```
