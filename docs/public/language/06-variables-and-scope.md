# 6. Variables and scope

## 6.1. Local variables

Local variables are declared with `var`.

A declaration MUST include an explicit type annotation, an initializer, or both.

When an initializer is absent, an explicit type annotation is REQUIRED.

When a type annotation is absent, the variable type is inferred from the initializer.

The inferred type of the `null` literal alone is invalid because `null` has no denotable type.

An uninitialized local variable has no default value. Every read MUST be proven by definite-assignment analysis to occur after an assignment on every reachable control-flow path.

Local variables are mutable.

> The first variable has an explicit type, while the second infers `int`:
>
> ```text
> var total: number = 1;
> var count = 2;
> count = count + 1;
> ```
>
> Given a host global `condition: bool`, this program is invalid because `result` is not definitely assigned on every path:
>
> ```text
> var result: int;
> if (condition) {
>     result = 1;
> }
> return result;
> ```

## 6.2. Scope

A program body, block, and `for` statement initializer establish lexical scopes as defined by the statement grammar.

A local variable name MUST NOT duplicate a declaration in the same scope. Whether it may shadow a variable from an enclosing scope or the static environment depends on the language profile as defined in [Section 14.7](14-language-profiles.md#147-shadowing).

Function names and variable names occupy distinct namespaces because functions are not first-class values and can only occur in call position.

Top-level user-defined functions are visible throughout the complete program, including within functions declared earlier. Direct and mutual recursion are available only when enabled by the language profile.

The complete user-function contract is defined in [Section 7](07-user-defined-functions.md). Its availability and recursion depend on the language profile as defined in [Section 14.1](14-language-profiles.md#141-user-defined-functions-and-recursion).

Function parameters establish the root variable scope of their function body. Parameters are definitely assigned and immutable. Shadowing involving parameters follows the selected language profile.

Function bodies cannot access locals declared by top-level executable statements or by other functions.

> For example, `inside` is not visible after its block:
>
> ```text
> {
>     var inside = 1;
> }
> return inside;
> ```
>
> This produces a compile-time name-resolution error independently of the selected shadowing policy.

## 6.3. Global variables

Global variables are declared by the host environment.

Global bindings are read-only from MuLang source. If a global value is an object or array, its contents MAY still be mutated through the supported property and element operations.

The availability of those mutation operations depends on the language profile as defined in [Section 14.5](14-language-profiles.md#145-mutations).

> Given a host global `values` of type `int[]`, this program may mutate an element but cannot assign a new array to the global binding:
>
> ```text
> values[0] = 1;
> values = [2];
> ```
>
> The second statement produces a compile-time error.
