# Roadmap

## Before MuLang `1.2`

1. Add the [`primitive` abstract type](primitive-abstract-type.plan.md) as the common non-null supertype of all primitive types.

   **Completed for `0.3.0-alpha.1`.**
2. Add readonly and optional object properties.

   **Completed for `0.3.0-alpha.2`.**
3. Warn in earlier language versions when identifiers reserved by `1.2`, including `primitive`, `error`, `try`, `catch`, `finally`, and `throw`, are used.

   The `error` migration warning remains to be added with the runtime error type
   in `0.3.0-alpha.3`; the other warnings are complete.
4. Stabilize the host and provider error contracts before defining source-level exception handling.

   **Completed for `0.2.0-alpha.7`.**
5. Implement [scoped IR slots](scoped-ir-slots.plan.md) through explicit lifetime regions, preserving read-only capability and definite-assignment semantics across re-entered lexical scopes.
6. Extend the unreleased MuIR version 2 with scoped slots and exception-handling regions without changing the `primitive` token introduced by `0.3.0-alpha.1` or the property capability tokens introduced by `0.3.0-alpha.2`.

   Points 5 and 6 are coordinated for `0.3.0-alpha.3`: lifetime regions are
   implemented first, followed by end-to-end exception-region support on the
   same revised MuIR 2 contract.
7. Design source-level exception handling on top of the revised type and IR models.
8. Keep language-version availability distinct from feature flags.

## Future steps

1. Add language-server workspace configuration for the language profile, host environment, and expected result type.

Comments remain low priority. User-defined types, first-class functions, and function types should be deferred to later dedicated releases.

Language-server workspace configuration, completion, hover, navigation, rename, and formatting are not release blockers.
