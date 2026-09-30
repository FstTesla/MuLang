# 6. Variables and scope

## 6.1. Local variables

Local variables are declared with `var`.

A declaration MUST include an explicit type annotation, an initializer, or both.

When an initializer is absent, an explicit type annotation is REQUIRED.

When a type annotation is absent, the variable type is inferred from the initializer.

The inferred type of the `null` literal alone is invalid because `null` has no denotable type.

An uninitialized local variable has no default value. Every read MUST be proven by definite-assignment analysis to occur after an assignment on every reachable control-flow path.

Local variables are mutable unless the declaration name is followed by `$`.

A read-only local variable may be initialized by its declaration or assigned later when an explicit type annotation is present. Its declaration initializer, when present, counts as its assignment.

Every reachable control-flow path may assign a read-only local at most once. Assignments in mutually exclusive branches are valid. A later assignment is invalid when the variable may already have been assigned on any incoming path, including when it is definitely assigned on only some incoming paths.

A read-only local declared outside a loop may be assigned inside the loop only when no path following that assignment reaches a back-edge of that loop or any enclosing loop that retains the same variable instance. An assignment followed by a mandatory exit from all applicable loops is valid.

A read-only local declared inside a loop denotes a new variable instance for each execution of its declaration and follows the ordinary single-assignment rule within that iteration.

The `$` is a declaration modifier and is not part of the variable name.

> The first variable has an explicit type, while the second infers `int`:
>
> ```text
> var total: number = 1;
> var count = 2;
> count = count + 1;
> ```

> Both read-only declarations are valid. `immediate` is initialized by its declaration, while `delayed` is assigned later and therefore requires an explicit type:
>
> ```text
> var immediate$ = 1;
> var delayed$: int;
> delayed = 2;
> return immediate + delayed;
> ```
>
> The program returns `3`.

> Given a host global `condition: bool`, mutually exclusive branches may assign the same read-only local:
>
> ```text
> var result$: int;
> if (condition) {
>     result = 1;
> } else {
>     result = 2;
> }
> return result;
> ```
>
> This program is valid because every path assigns `result` exactly once before it is read.

> This program is invalid because the initializer is the first assignment and the following statement attempts a second assignment:
>
> ```text
> var value$ = 1;
> value = 2;
> ```
>
> The second assignment produces a compile-time error.

> Given a host global `condition: bool`, this program is invalid even though `value` is not definitely assigned after the `if`:
>
> ```text
> var value$: int;
> if (condition) {
>     value = 1;
> }
> value = 2;
> ```
>
> The final assignment produces a compile-time error because an incoming path may already have assigned `value`.

> Given a host global `condition: bool`, this program is invalid because a read-only local declared outside a loop cannot be assigned by the loop:
>
> ```text
> var value$: int;
> while (condition) {
>     value = 1;
> }
> ```
>
> The assignment in the loop produces a compile-time error.

> This program is valid because the `break` prevents control from reaching another iteration after the assignment:
>
> ```text
> var value$: int;
> while (true) {
>     value = 1;
>     break;
> }
> return value;
> ```
>
> The program returns `1`.

> A read-only local declared inside a loop instead denotes a new variable instance on each iteration:
>
> ```text
> while (condition) {
>     var value$: int;
>     value = 1;
> }
> ```
>
> This program is valid.

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
