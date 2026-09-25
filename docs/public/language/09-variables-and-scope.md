# 9. Variables and scope

## 9.1. Local variables

Local variables are declared with `var`.

A declaration MUST include an explicit type annotation, an initializer, or both.

When an initializer is absent, an explicit type annotation is REQUIRED.

When a type annotation is absent, the variable type is inferred from the initializer.

The inferred type of the `null` literal alone is invalid because `null` has no denotable type.

An uninitialized local variable has no default value. Every read MUST be proven by definite-assignment analysis to occur after an assignment on every reachable control-flow path.

Local variables are mutable.

## 9.2. Scope

A program body, block, and `for` statement initializer establish lexical scopes as defined by the statement grammar.

A local variable name MUST NOT match any local or global variable name visible at the declaration point.

Local variable shadowing is not supported, including shadowing of global variables.

The permitted forms of shadowing depend on the language profile as defined in [Section 18.8](18-language-profiles.md#188-shadowing).

Function names and variable names occupy distinct namespaces because functions are not first-class values and can only occur in call position.

Top-level user-defined functions are visible throughout the complete program, including within functions declared earlier. Direct and mutual recursion are supported.

The complete user-function contract is defined in [Section 23](23-user-defined-functions.md). Its availability and recursion depend on the language profile as defined in [Section 18.2](18-language-profiles.md#182-user-defined-functions-and-recursion).

Function parameters establish the root variable scope of their function body. Parameters are definitely assigned, immutable, and cannot be shadowed by local or global variables.

Function bodies cannot access locals declared by top-level executable statements or by other functions.

## 9.3. Global variables

Global variables are declared by the provider.

Global bindings are read-only from MuLang source. If a global value is an object or array, its contents MAY still be mutated through the supported property and element operations.

The availability of those mutation operations depends on the language profile as defined in [Section 18.6](18-language-profiles.md#186-mutations).
