# Autopilot Implementation Choices

This document records important autonomous choices made while implementing MuLang.

## Portable IR

- The portable IR uses mutable numbered slots and explicit basic blocks rather than SSA. This keeps loop lowering and translation to .NET expression trees straightforward while remaining runtime-independent.
- Every computed value is stored in a typed slot. Instructions refer only to slots, symbolic provider IDs, portable operators, types, and source spans.
- Short-circuit Boolean operators and conditional expressions are lowered to branches that assign a shared result slot.
- The compiler, portable IR, and .NET exporter are compiled into the single `MuLang` assembly. Their contracts remain internal without production `InternalsVisibleTo` relationships; only the test assembly receives internal access.

## .NET exporter

- The initial .NET backend stores slot values as `object` and centralizes language semantics in runtime operation helpers. This prioritizes semantic correctness and backend simplicity over specialized primitive expression trees.
- The compiled delegate shape is `Func<DotNetRuntimeContext, object?>`. A future public facade can provide typed wrappers after validating the requested result type.
- The first public facade exposes untyped expression and program compilation results around that delegate. Generic typed wrappers are deferred until the public API and runtime value conversion policy are reviewed.
- Provider functions are invoked through stable symbolic IDs and a runtime delegate accepting an ordered read-only argument list.
- Global values, provider return values, and adapter reads are checked against the statically expected MuLang type at their runtime boundary.
- Provider boundary checks are shallow: they validate the immediate runtime kind, while individual property and element values are validated when read. Deep `is`, checked `as`, and structural equality operations charge the execution budget during traversal.
- Provider objects and arrays must implement the explicit public adapter interfaces `IDotNetObjectValue` and `IDotNetArrayValue`. Convenience wrappers for common CLR collections or POCOs are intentionally deferred.
- Runtime primitive representations are fixed as `bool`, `long`, `double`, and `string`. Providers must adapt other CLR numeric representations explicitly.
- Execution budget is charged once per executed IR instruction or terminator and once more for each provider function invocation. Cancellation is checked at the same boundaries.
- `long.MaxValue` is the explicit unlimited-budget sentinel for `DotNetRuntimeContext`.
