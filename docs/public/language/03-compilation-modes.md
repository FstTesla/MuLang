# 3. Compilation modes

Every compilation MUST explicitly select one of the following modes.

## 3.1. Expression mode

Expression mode accepts exactly one expression followed by the end of the source text.

The expression value is the result of execution. A void-returning function call is not valid as the root of expression mode.

Statements, including `return`, are not valid in expression mode.

## 3.2. Program mode

Program mode accepts a sequence of statements followed by the end of the source text.

Program mode MAY begin with zero or more top-level function declarations. All function declarations MUST precede executable statements.

The availability of user-defined function declarations and recursion depends on the language profile as defined in [Section 18.2](18-language-profiles.md#182-user-defined-functions-and-recursion).

A pure expression is not a statement. A function call is the only expression permitted as an expression statement.

A non-void program MUST return a value on every reachable path. A void program MAY complete without an explicit `return`.
