# Roadmap

## Before `0.2.0`

1. Stabilize the host-facing runtime error model:
   - structured runtime errors, categories, recoverability, and MuLang stack;
   - `ExecutionResult` alongside the exception-based API;
   - an explicit provider-error contract.

   **Completed for `0.2.0-alpha.7`.**
2. Introduce the canonical provider invocation context for structural operations, cancellation, and runtime error reporting.

   **Completed for `0.2.0-alpha.7`.**
3. Implement an initial optional standard library on top of these contracts.

   **Completed for `0.2.0-alpha.7`.**
4. Add language-server workspace configuration for the language profile, host environment, and expected result type.

Completion, hover, navigation, rename, and formatting are not release blockers.

## Before MuLang `1.2`

1. Add the [`primitive` abstract type](primitive-abstract-type.plan.md) as the common non-null supertype of all primitive types.
2. Add readonly and optional object properties.
3. Warn in earlier language versions when identifiers reserved by `1.2`, such as `primitive`, `try`, `catch`, and `throw`, are used.
4. Stabilize the host and provider error contracts before defining source-level exception handling.
5. Implement [scoped IR slots](scoped-ir-slots.plan.md) through explicit lifetime regions, preserving read-only capability and definite-assignment semantics across re-entered lexical scopes.
6. Coordinate the new intrinsic type, scoped slots, and exception-handling regions in the next MuIR format version to avoid consecutive incompatible format revisions.
7. Design source-level exception handling on top of the revised type and IR models.
8. Keep language-version availability distinct from feature flags.

Comments remain low priority. User-defined types, first-class functions, and function types should be deferred to later dedicated releases.
