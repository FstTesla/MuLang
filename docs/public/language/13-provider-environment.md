# 13. Provider environment

## 13.1. Static environment

The provider supplies an immutable static environment containing:

- global variable declarations;
- function declarations;
- structured object declarations;
- stable symbolic identifiers.

Every structured object type referenced directly or indirectly by a global, function parameter, function return type, array element, nullable type, or structured property MUST be registered in the static environment. Every reference to the same stable type identifier MUST resolve to the same type declaration.

Profile compatibility requirements for provider-declared open structured types are defined in [Section 18.5](18-language-profiles.md#185-open-objects).

The compiler resolves source names exclusively against local declarations and the static environment.

## 13.2. Runtime context

Execution receives a runtime context compatible with the static environment used during compilation.

The runtime context supplies:

- global values;
- function implementations;
- object and array adapters;
- cancellation;
- execution budget state.

The runtime context MUST expose no operation that was not declared by the static environment or required by the language runtime contract.

## 13.3. Environment compatibility

The static environment MUST have a deterministic fingerprint.

A compiled program MUST reject execution with a runtime context whose environment fingerprint is incompatible.

Names, types, mutability capabilities, and function signatures that affect compilation MUST contribute to compatibility.

Array capability contributes to type canonicalization, so mutable and read-only array signatures have distinct environment fingerprints.

Provider implementations are trusted host code. A read-only array parameter restricts operations in MuLang source and participates in runtime shape validation, but the .NET provider receives the original adapter object. A provider MAY therefore observe and deliberately use additional host capabilities exposed by that object.
