# 23. User-defined functions

User-defined functions are synchronous top-level program declarations. They are not first-class values.

## 23.1. Availability and placement

User-defined functions are available only in program mode and only when enabled by the selected language profile.

A program MAY begin with zero or more function declarations. Every function declaration MUST precede every executable top-level statement.

Functions cannot be declared in expression mode, inside another function, or inside a statement block.

When the feature is disabled, declarations and calls remain recognizable for recovery and produce the diagnostics defined by the selected profile.

## 23.2. Declarations and signatures

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
- a provider function name.

Function names and variable names occupy distinct namespaces. Function names are resolved only in call position.

## 23.3. Visibility and parameter scope

Every valid top-level function declaration is visible throughout the complete program, independently from textual declaration order. Forward calls are therefore valid.

Function parameters establish the root variable scope of the function body.

Parameters:

- are definitely assigned when the function is entered;
- are immutable;
- cannot be assignment targets;
- cannot be shadowed by local or global variables.

Function bodies cannot capture locals declared by top-level executable statements or by another function. All data entering a function is supplied through parameters or provider globals.

Local declarations inside the body follow the ordinary block and shadowing rules.

## 23.4. Calls

A call supplies exactly one argument for every declared parameter, in declaration order.

Each argument MUST be statically assignable to the corresponding parameter type.

A non-void call is an expression and MAY also be used as a call statement, in which case its result is discarded.

A void call is valid only as a call statement or in another context that explicitly accepts a void operation.

The compiler MUST assume that every call can have observable side effects.

User-defined functions are not values: they cannot be stored in variables, passed as arguments, returned, placed in objects or arrays, or accessed without invocation syntax.

## 23.5. Returns and control flow

`return` exits the current function.

A non-void function requires a return expression assignable to its declared return type. Every reachable path MUST return a value.

A void function permits only `return` without an expression and MAY reach the end of its body, which is equivalent to returning without a value.

`break` and `continue` cannot cross a function boundary.

Definite-assignment analysis is performed independently for each function.

## 23.6. Recursion

Direct and mutual recursion are supported when enabled by the selected language profile.

When recursion is disabled, the compiler constructs the user-function call graph and rejects every strongly connected component that:

- contains more than one function; or
- contains a self-edge.

Provider calls do not add recursion edges to the user-function call graph.

The recursion setting is dormant when user-defined functions are disabled.

Runtime execution enforces the configured maximum user-function call depth.

## 23.7. Portable IR

Every user-defined function lowers to one `IrFunction` with:

- a compiler-assigned stable function ID;
- an explicit return type;
- contiguous leading parameter slots;
- function-local local and temporary slots;
- one entry block and zero or more additional basic blocks.

Parameter slots are read-only, are implicitly defined at function entry, and have no instruction definition site.

Calls use `IrInstruction.UserCall` and reference the target function ID. The IR validator requires argument count and types, return type, and destination shape to match the target function.

Each function has independent slot and control-flow namespaces. No IR instruction may reference a slot or block owned by another function.
