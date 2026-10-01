# 1. Overview

MuLang is a small, embeddable language for validating and executing expressions and imperative programs supplied as text.

## 1.1. Design goals

The language is designed to:

- support expression-only and statement-based compilation;
- use a small, explicitly defined type system;
- obtain all global variables, functions, and structured types from a host-provided environment;
- prevent implicit access to host-specific operations;
- produce deterministic diagnostics before execution whenever possible;
- preserve the same language semantics across conforming implementations;
- allow controlled mutation of host-supplied objects and arrays;
- remain suitable for execution with cancellation and resource limits.

This specification defines MuLang without requiring knowledge of another language or platform.

## 1.2. Non-goals

Language version 1 does not provide:

- user-defined structured types;
- classes, inheritance, interfaces, or generics;
- function values;
- asynchronous functions;
- function overloads;
- optional or variadic function parameters;
- tuples;
- heterogeneous arrays;
- general union types;
- exception handling in source code;
- reflection or implicit access to host members;
- a privileged or implicitly imported standard library;
- comments;
- flow-sensitive type narrowing;
- assignment expressions.

## 1.3. Compilation modes

Every compilation MUST select expression mode or program mode.

### 1.3.1. Expression mode

Expression mode accepts exactly one expression followed by the end of the source text.

The expression value is the result of execution. A void-returning function call is not valid as the root expression.

Statements, including `return`, are not valid in expression mode.

> For example, this expression produces the `int` value `7`:
>
> ```text
> 1 + 2 * 3
> ```

### 1.3.2. Program mode

Program mode accepts zero or more top-level function declarations followed by zero or more executable statements and the end of the source text. A function declaration MUST NOT follow an executable top-level statement.

User-defined functions are specified in [Section 7](07-user-defined-functions.md). Their availability and recursion depend on the language profile as defined in [Section 14.1](14-language-profiles.md#141-user-defined-functions-and-recursion).

A pure expression is not a statement. A function call is the only expression permitted as an expression statement.

A non-void program MUST return a value on every reachable path. A void program MAY complete without an explicit `return`.

> For example, this non-void program produces the `int` value `7`:
>
> ```text
> var value = 1 + 2 * 3;
> return value;
> ```
>
> The following is invalid because the pure expression is not a statement:
>
> ```text
> 1 + 2;
> return 3;
> ```
