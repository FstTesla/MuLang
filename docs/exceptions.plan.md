# Exception Handling Plan

## Goals

Introduce a structured runtime error model and source-level recovery mechanism.

The design must:

- distinguish recoverable application failures from execution-aborting failures;
- represent errors independently from exceptions raised by the host platform;
- preserve the available diagnostic information;
- support errors originating from operations, statements, user-defined functions,
  and provider functions;
- avoid breaking changes where they are not required;
- define explicit compatibility boundaries for both MuLang and MuIR.

The following designs are explicitly out of scope:

- making every runtime error catchable;
- checked exceptions declared in function signatures;
- an effect system for statically propagating errors;
- making fallible operations return `result<T>`.

## Implementation Status

Release `0.2.0-alpha.7` implements the host-facing foundation:

- structured `RuntimeError` values with categories, catchability, source spans,
  MuLang stack frames, causes, and data;
- `ExecutionResult` alongside exception-based execution;
- the explicit `MuLangProviderException` application-error contract;
- the canonical context-aware `DotNetProviderFunction` and
  `DotNetProviderInvocationContext` APIs.

Source-level `try`, `catch`, `throw`, and `finally` remain deferred to the
MuLang 1.2 work.

Release `0.3.0-alpha.3` implements the built-in `error` type,
exception-region execution model, revised MuIR 2 representation, and .NET
runtime support, coordinated with scoped IR slots. Protected-statement syntax
and binding follow after that foundation.

## Pre-`0.2.0-alpha.7` State

Before `0.2.0-alpha.7`, `MuLangRuntimeException` represented an execution
failure and contained:

- an error code;
- a message;
- a source span;
- an optional underlying host exception.

That model had several limitations:

- an error is not a MuLang value;
- the exception does not expose the category required by the specification;
- recoverable errors are not distinguished from execution-aborting failures;
- no MuLang call stack is preserved;
- every non-MuLang exception raised by a provider function becomes a generic
  `MUL6014` error;
- MuIR cannot represent `throw`, protected regions, or handlers.

Adding only `try` and `catch` syntax would therefore be insufficient. The
feature requires coordinated changes to parsing, binding, lowering, MuIR,
validation, serialization, and exporters.

## Structured Runtime Error Model

Introduce a host-side error object that is independent from the .NET exception
used to propagate it:

```text
RuntimeError
- Code
- Category
- Message
- Span
- Frames
- Catchability
- Cause
- Data presence
- Data
```

`MuLangRuntimeException` remains the mechanism used to propagate failures
through the .NET call stack, but exposes a `RuntimeError`.

`Cause` is an optional `RuntimeError` selected explicitly by the creator of the
error. It represents a public relationship between MuLang errors and is not an
automatic projection of `Exception.InnerException`. An underlying host
exception remains separate diagnostic information available only to the host.

### Error Categories

The initial categories are:

| Category | Failures |
|---|---|
| `Operation` | Division by zero, invalid index, failed cast |
| `Mutation` | Rejected property or element mutation or removal |
| `Application` | An application failure intentionally raised by MuLang code or a provider function |
| `Provider` | An unexpected failure in a provider implementation |
| `Resource` | Exhausted execution budget or call depth |
| `Cancellation` | Cancelled execution |
| `Environment` | Incompatible environment or missing runtime binding |
| `RuntimeContract` | A provider value that violates its declared runtime contract |

Catchability is an explicit error property. It must not be inferred from the
numeric code or category.

`Application` and `Provider` represent different concepts. `Application`
describes an expected domain failure intentionally communicated to the program.
`Provider` describes an unexpected implementation failure in host code.
`Application` errors are normally catchable; `Provider` errors are not.

The category does not encode the precise origin of an error. The source span,
MuLang stack, and host diagnostic metadata can identify whether an
`Application` error originated in MuLang code or a provider function.

This model can be introduced without changing the source language. The existing
`MuLangRuntimeException` code/message/span constructor remains temporarily
available as an obsolete compatibility API; new code constructs a
`RuntimeError` explicitly.

## Host Execution Result

Add a host-facing API that executes MuLang code without requiring callers to
catch .NET exceptions:

```text
ExecutionResult
- IsSuccess
- Value
- Error
```

`ExecutionResult` improves host integration but does not itself provide
source-level recovery.

The result-based host API coexists with the current delegate-and-exception API,
avoiding an immediate breaking change.

## Catchability Policy

Normally catchable failures include:

- failed casts;
- division by zero;
- arithmetic overflow;
- invalid indices;
- absent properties;
- rejected mutation or removal;
- declared application failures from provider functions;
- errors explicitly raised by MuLang code.

Failures that continue to abort execution include:

- cancellation;
- exhausted execution budget;
- exhausted user-function call depth;
- an incompatible runtime environment;
- a missing provider implementation;
- a runtime value that violates its static contract;
- an unexpected provider implementation failure;
- an internal compiler, exporter, or runtime failure.

Allowing a program to catch budget exhaustion would let it continue after
exceeding a host-imposed limit. The same concern applies to cancellation,
environment incompatibility, and runtime contract violations.

## Provider Function Failures

The pre-`0.2.0-alpha.7` implementation converted every provider exception into
`ProviderFailure`. The implemented model distinguishes expected application
failures from unexpected implementation failures.

### Declared Application Failure

A provider function can report an expected failure through:

- a dedicated host exception such as `MuLangProviderException`; or
- a structured failure mechanism defined by the provider invocation contract.

The resulting MuLang error has category `Application` and is catchable.

### Unexpected Provider Failure

An unexpected exception, such as an internal provider implementation fault,
produces an uncatchable `Provider` error. MuLang code receives only sanitized
information, while the host retains access to the original exception.

This prevents provider programming errors from accidentally becoming ordinary
MuLang control flow.

The runtime should also avoid indiscriminately catching host exceptions that
the platform considers fatal or unrecoverable.

## Language Model

The new language version introduces protected statements with a handler, a
cleanup block, or both:

```text
try {
    ...
} catch (error) {
    ...
}

try {
    ...
} finally {
    ...
}

try {
    ...
} catch (error) {
    ...
} finally {
    ...
}
```

Every `try` has at least one `catch` or `finally` clause. A `catch` clause, when
present, appears before `finally`. Multiple handlers and multiple cleanup
clauses are not permitted.

The catch parameter is optional:

```text
try {
    ...
} catch {
    ...
}
```

When present, the parameter:

- has the built-in type `error`;
- is assignable to `object`;
- is a read-only binding.

The initial handler model has no filters, multiple handlers, or error type
hierarchy.

### The `error` Type

MuLang 1.2 reserves `error` as a keyword. Versions 1 and 1.1 continue treating
it as an identifier and produce the future-reserved-keyword warning.

Rename the internal compiler recovery sentinel canonically to
`TypeSymbols.ErrorRecovery` and `TypeKind.ErrorRecovery`. Preserve
`TypeSymbols.Error` and `TypeKind.Error` as obsolete compatibility aliases with
the same value and semantics; they never represent a runtime error value.

The executable built-in error value uses `TypeSymbols.ErrorValue` and
`TypeKind.ErrorValue`, with source display name and MuIR token `error`. The
host diagnostic model remains the `RuntimeError` class, while
`IDotNetErrorValue` represents a MuLang error value at the .NET boundary.

`ErrorValue` is an intrinsic non-null type assignable to `object` and `unknown`.
It may participate in nullable, array, and object-property types under the
ordinary type rules. It is available to MuLang 1.2 environment and IR contracts
independently from the source-level exception-handling feature flag.

`ErrorValue` also has a special implicit structural-view conversion. It is
assignable to a structured-object target when its closed built-in property
shape is structurally compatible with that target under the ordinary property
rules. Compatibility therefore considers property names, value types,
optionality, read-only capability, and target openness.

Because every built-in error property is read-only, an `ErrorValue` cannot
satisfy a mutable target property. Required built-in properties may satisfy
optional read-only target properties. Optional `cause` and `data` properties
cannot satisfy required target properties. The built-in error shape is closed,
so the ordinary openness and known-property-set requirements apply without a
special width-conversion rule.

The conversion is a read-only view and preserves the original error value,
wrapper, underlying `RuntimeError`, property presence, `cause`, and `data`
identity. It does not normalize or copy the value. The reverse conversion from
an arbitrary compatible structured object to `error` is not implicit; checked
conversion and `throw` prototype normalization remain separate operations.

The built-in `error` type exposes exactly:

- `code$: string`;
- `category$: string`;
- `message$: string`;
- `cause$?: error`;
- `data$?: unknown?`;
- `spanStart$: int`;
- `spanLength$: int`.

All properties of `error` are declared read-only through the general read-only
object-property model. Error values do not introduce special mutability rules.
This part therefore depends on the specification and implementation of
read-only object properties.

Category strings use lower camel case:

- `operation`;
- `mutation`;
- `application`;
- `provider`;
- `resource`;
- `cancellation`;
- `environment`;
- `runtimeContract`.

Catchability and MuLang stack frames remain available to .NET providers through
the underlying `RuntimeError` but are not object properties visible to MuLang.

`cause` is populated at the discretion of the runtime, provider, or MuLang code
that creates the error. The property is optional but, when present, contains a
non-null `error` value. It is not derived automatically from the host exception
chain.

`data` is an optional channel for arbitrary application information. The
payload preserves its representation, identity, and mutability. The `data`
property itself is read-only, but an object or array stored in it does not
become deeply immutable.

Absent `data` and present `data` with a `null` value are distinct runtime
states. Introduce a public `RuntimeErrorData` value type with presence and value
components. Add compatible overloads so existing APIs continue interpreting a
plain `null` data argument as absence while the new value type can express
present-null.

### .NET Error Values

The .NET exporter represents a MuLang `error` through a public
`IDotNetErrorValue` interface. The interface extends `IDotNetObjectValue` and
`IDotNetObjectPropertyCapabilities` and exposes:

```text
RuntimeError Error
```

The canonical implementation wraps a `RuntimeError`. Providers may supply
their own implementations. Provider implementations are validated against the
closed seven-property shape, including property values, optional presence, and
read-only capabilities. Additional properties are rejected. A validated
provider wrapper is preserved rather than replaced by a canonical wrapper.

Multiple wrappers may reference the same `RuntimeError`. MuLang identity
comparison uses reference identity of the underlying `RuntimeError`, not
wrapper identity. Structural equality compares the seven object properties
through the ordinary object equality rules.

### Catch Semantics

The initial semantics are:

- `catch` handles only errors explicitly marked as catchable;
- completed side effects are not rolled back;
- an error raised by the handler propagates normally;
- a `try` may have one `catch`, one `finally`, or both;
- the catch parameter is optional and read-only when present;
- handlers have no filters or type hierarchy.

The lack of rollback is intentional. Effects completed by a statement or
provider function before an error remain observable.

### `finally` Semantics

`finally` is reserved in MuLang 1.2 but may be implemented after the initial
`try` and `catch` milestone. Its syntax and semantics are defined in advance so
that the later addition does not require another language version.

A `finally` block executes after the protected block and, when present, after
the matching handler. It executes when either preceding block completes:

- normally;
- by returning from the current function;
- by breaking or continuing to a target outside that block;
- by raising a catchable error.

An uncatchable error bypasses `catch` and `finally`. This includes cancellation,
resource exhaustion, environment incompatibility, runtime-contract failures,
unexpected provider failures, and internal runtime failures. User cleanup code
must not run after the host has required execution to abort or after its
execution budget has been exhausted.

If both `catch` and `finally` are present, a catchable error from the protected
block is handled first and `finally` then executes after the handler. A
catchable error raised by the handler also executes `finally` before
propagating. A `try` with only `finally` preserves a pending catchable error and
resumes its propagation after successful cleanup.

The pending completion is resumed when `finally` completes normally. A
catchable or uncatchable error raised by `finally` replaces any pending normal
completion, return, loop transfer, or error. The replaced error or completion
is retained only in internal runtime diagnostics; no new public host API
exposes it, and it is not assigned automatically to the public `cause`
property. The `catch` associated with the same `try` does not handle an error
raised by its `finally`; an enclosing protected statement may handle it when
it is catchable.

`return` is not permitted anywhere within a `finally` block, including within
loops or nested lexical blocks owned by that cleanup block. `break` and
`continue` may target a loop declared within the same `finally` block, but may
not target a loop outside it.

The catch parameter and the implicit current error used by `throw;` are scoped
to the handler. They are not implicitly available in the sibling `finally`
block. A value that must be observed during cleanup must be stored explicitly
in a binding visible to both clauses.

### Binding and Control-Flow Analysis

The parser represents `catch` and `finally` as optional clauses and reports an
error when both are absent, when they are repeated, or when `catch` follows
`finally`.

The binder tracks whether the current statement is within a `finally` block.
It rejects every `return` in that context. It also records the loop target of
each `break` and `continue` and rejects the transfer when the target loop was
entered before the active `finally` block.

Definite-assignment and reachability analysis model `finally` as a mandatory
step only for completions that remain within MuLang control flow. Handler and
cleanup entry states use the conservative intersection of every path that can
reach them. Every IR instruction is treated as potentially raising a catchable
error for this analysis. An instruction destination is defined only on the
successful continuation. Paths terminated by an uncatchable error do not
contribute a continuing state.

A `finally` block is reachable whenever its protected block or handler has a
normal or catchable abrupt completion. The statement following the protected
statement is reachable only when at least one such completion remains normal
after cleanup.

### Lowering

Lowering uses one shared cleanup path rather than duplicating the `finally`
body at every exit. Before leaving the protected block or handler, it records a
pending completion with one of these forms:

- normal continuation;
- function return with its value, when any;
- loop transfer with its resolved target;
- catchable error propagation.

Portable IR keeps `Jump`, `Branch`, and `Return` and interprets them relative
to the source and target exception-region memberships. `Throw` represents
explicit error propagation. A cleanup completes normally with `Resume`, which
continues the pending completion.

For `Jump`, the target determines the exception regions being exited. For
`Branch`, the condition is evaluated and one target is selected before cleanup;
the selected edge independently determines its cleanup chain. The true and
false edges may therefore remain inside different nesting levels or exit
through different cleanup chains without bridge blocks.

`Return` derives its cleanup chain from the source block and captures its value
before lifetime regions are deactivated and before cleanup begins. `Throw`
derives handler and cleanup behavior from the source exception part and the
error catchability.

The cleanup chain is derived from the exception-region hierarchy rather than
repeated on a terminator. Errors raised during cleanup replace the pending
completion.

Transfers wholly contained within a loop declared inside `finally` remain
ordinary control-flow edges and do not use the pending-completion dispatcher.
No source construct can create a return completion while lowering `finally`
because the binder rejects it.

Uncatchable errors are never converted into pending completions and never
branch to the cleanup region. The runtime and exporters must preserve this
distinction even on targets whose native exception handling would normally
execute a platform `finally` block for every exception.

Cleanup locals belong to an explicit cleanup lifetime region. Each entry
creates a new activation, and ordinary control-flow edges cannot enter the
region except through its entry. This relies on the scoped-slot region model
rather than treating cleanup locals as function-scoped storage.

## Explicit Throwing

`throw` accepts an object expression that is structurally compatible with an
error prototype:

```text
throw {
    code: "invalid-value",
    message: "The value is invalid."
};
```

The prototype has the following contextual shape:

- required `code: string`;
- required `message: string`;
- optional `cause`, accepting `error`, a compatible error prototype, or `null`;
- optional `data: unknown?`.

When present, `cause` can be:

- `null`;
- an existing `error` value;
- another object structurally compatible with the error prototype.

The binder validates prototype compatibility recursively.

The operand is not limited to an inline object literal. Any expression with a
statically compatible object type, including a local variable or function
result, can be passed to `throw`.

Prototype compatibility is specific to `throw`. It requires only the recognized
properties to be present and compatible; additional properties do not make the
prototype incompatible.

For an object literal operand, every unrecognized property produces a warning
at that property. Additional properties on local variables, function results,
and other object expressions do not produce diagnostics.

The recursive prototype is a contextual form understood by `throw`, not a new
source-visible generic type or intrinsic constructor.

### Runtime Normalization

At runtime, `throw` normalizes the prototype into a well-formed `error` value:

- `code` and `message` are copied;
- the category is always set to `Application`;
- the MuLang source span and stack are added;
- an absent or `null` cause results in an absent `cause` property;
- an existing `error` cause is reused;
- a prototype cause is normalized recursively;
- `data`, when present, is copied while preserving its value and identity.

The resulting `error` is not a mutable alias of the prototype.

The runtime silently ignores every unrecognized property. It does not enumerate
the property, read its value, or copy it into the resulting `error`. The final
object exposes only properties declared by the `error` type. Additional
prototype properties cannot influence the category or other runtime-owned
metadata.

### Rethrow

Inside a `catch`, the current error can be rethrown:

```text
throw;
```

MuLang code cannot create errors in runtime-reserved categories such as
`Cancellation` or `Environment`. Every new error explicitly thrown by MuLang
code has category `Application`.

## Language Compatibility

MuLang 1.2 reserves `error`, `try`, `catch`, `throw`, and `finally`, even though
source-level exception handling remains deferred beyond the portable IR
foundation.

Exception handling therefore belongs to a new language version, most likely
MuLang 1.2. Versions 1 and 1.1 continue to tokenize these names according to
their existing rules, preserving source compatibility.

An exception-handling language-profile option may additionally control feature
availability. Such an option remains subordinate to a language version that
defines the syntax.

## MuIR Compatibility

MuIR version 1 cannot represent exception handling. Exception regions extend
MuIR version 2 because version 2 has not entered a stable release. Existing
prerelease MuIR 2 documents are intentionally incompatible and rejected.
MuIR version 1 remains readable by synthesizing lifetime root region `0` and
zero exception regions.

Lifetime regions and exception regions are separate, coordinated hierarchies.
Every block directly owns one lifetime-region ID. Exception regions list their
directly owned blocks instead of adding redundant exception metadata to basic
blocks.

Introduce one `IrExceptionRegion` record with:

- a contiguous, zero-based function-local ID in the exception-region
  namespace;
- an optional parent exception-region ID;
- an explicit parent part identifying `Protected`, `Handler`, or `Cleanup`;
- one required `IrExceptionProtectedRegion` component;
- one optional `IrExceptionHandler` component;
- one optional `IrExceptionCleanup` component.

Each component declares an explicit entry block and a block-ID collection.
`IrExceptionHandler` additionally declares one mandatory read-only
`TypeSymbols.ErrorValue` slot. The handler always owns a dedicated lifetime
region containing that slot. The catch parameter, when present, binds to the
same slot. A handler without a source parameter still uses the slot for
`throw;`.

Component block collections have set semantics, contain only direct members,
and serialize in ascending block-ID order. Every block has zero or one direct
exception-region owner and part. Semantic membership in outer regions follows
the parent chain.

Two exception regions are either disjoint or strictly nested. A child region,
including all three of its parts, is entirely contained in exactly one part of
its parent. Part boundaries are independent from lifetime-region boundaries.
Every protected, handler, and cleanup component is single-entry.

Every exception region has at least one handler or cleanup component. Protected,
handler, and cleanup blocks are disjoint. Ordinary control-flow targets cannot
enter a handler or cleanup or otherwise bypass the region semantics.

The cleanup component receives an abstract pending completion rather than
source-visible mutable slots. MuIR retains the existing context-sensitive
control-flow terminators and adds:

- `throw` with an `ErrorValue` slot;
- `resume` with no operands.

`Jump`, `Branch`, and `Return` may cross exception-region boundaries. The
validator and exporter derive the exited regions and ordered cleanup chain from
the source block and selected destination. For `Branch`, each edge is analyzed
independently, and the selected target is fixed before cleanup begins.

`Return` captures its value before scoped slots are deactivated. `Resume` is
valid only in a cleanup component. `Return` is invalid in cleanup.

The validator derives the ordered cleanup chain from the exception-region
hierarchy. Terminators do not repeat region IDs or cleanup entries.

An `IrTerminator.Throw` propagates the `RuntimeError` represented by the
`IDotNetErrorValue` stored in its `ErrorValue` slot. Catchable errors select
the innermost applicable handler; uncatchable errors bypass handlers and
cleanup. Errors raised in a handler or cleanup are not handled by the handler
of the same exception region.

The validator enforces:

- separate contiguous lifetime-region and exception-region IDs;
- valid, non-overlapping direct exception ownership;
- proper nesting and explicit parent-part relationships;
- single-entry protected, handler, and cleanup components;
- presence of at least one handler or cleanup region;
- a mandatory read-only `ErrorValue` slot for every handler;
- dedicated handler lifetime ownership of the error slot;
- context-sensitive `Jump`, `Branch`, `Return`, and `Throw` behavior;
- no ordinary edge into a handler or cleanup entry;
- independent validation of both `Branch` edges;
- no `return` originating in a cleanup region;
- no cleanup-region loop transfer targeting a block outside that region;
- propagation of uncatchable errors without entering a handler or cleanup
  region;
- conservative definite-assignment state at handler and cleanup entries,
  treating every instruction as potentially raising a catchable error.

Every MuIR 2 function serializes lifetime and exception counts, lifetime root
region `0`, exception-region identifiers, parent and parent-part relationships,
all component entries and direct block sets, and handler error slots. Region,
slot, and block references remain function-local and are emitted in
deterministic identifier order. Reader options expose separate positive limits
for lifetime and exception regions.

The representation must remain independent from exception-handling facilities
provided by a target platform so that MuIR remains portable.

### Exporter and Runtime Requirements

The .NET exporter cannot map the feature directly to an unconditional CLR
`finally`, because CLR cleanup would also execute for MuLang errors whose
catchability requires immediate abortion. It must emit explicit cleanup control
flow and catch or filter only catchable `MuLangRuntimeException` instances that
need to become pending completions.

Normal exits and source-level returns or loop transfers branch through the
shared cleanup path. A catchable error raised by the protected block, handler,
or nested provider call is captured only long enough to execute cleanup and is
then propagated unless replaced. Uncatchable errors pass through without
executing MuLang cleanup code.

When cleanup replaces a pending completion, the runtime may retain the
displaced completion for internal tracing. No public API exposes it.
`RuntimeError.Cause` remains unchanged unless MuLang code or a provider
explicitly supplied it.

### `finally` Test Plan

Parser and binder tests cover:

- `try` with only `catch`, only `finally`, or both;
- missing, repeated, and incorrectly ordered clauses;
- `return` at every nesting depth within cleanup;
- internal and external `break` and `continue` targets;
- catch-parameter and rethrow scope at the cleanup boundary.

Control-flow, runtime, and exporter tests cover:

- normal completion of the protected block and handler;
- returns and external loop transfers passing through cleanup;
- internal cleanup loops retaining ordinary control flow;
- catchable errors from the protected block, handler, and cleanup;
- `try-finally` propagation of an unchanged catchable error;
- cleanup errors replacing pending returns, transfers, and errors;
- replacement of pending completions without changing `cause` or exposing the
  displaced completion publicly;
- uncatchable cancellation, resource, provider, environment, contract, and
  internal failures bypassing cleanup;
- nested protected statements and cleanup regions;
- completed side effects remaining observable in execution order.

MuIR tests cover malformed region nesting, illegal entries and exits,
cleanup-aware completion dispatch, deterministic serialization, and
round-tripping of handler-only, cleanup-only, and combined regions.

### Coordinated Implementation Sequence

1. Complete the lifetime-region implementation.
2. Add canonical `ErrorRecovery` names with obsolete `Error` aliases, then add
   `TypeKind.ErrorValue`, `TypeSymbols.ErrorValue`, `RuntimeErrorData`, and the
   `error` MuIR token.
3. Add `IDotNetErrorValue`, canonical wrapping, provider-wrapper validation,
   object properties, equality, and identity semantics.
4. Add exception-region component records and compatibility constructors.
5. Extend `IrBuilder` with a separate structured exception-region stack.
6. Add exception-region structural and dataflow validation.
7. Add revised MuIR 2 parsing, writing, limits, and rejection of the earlier
   prerelease MuIR 2 grammar.
8. Add `Throw` and `Resume`, and make `Jump`, `Branch`, and `Return`
   context-sensitive across exception-region boundaries.
9. Implement end-to-end .NET exporter and runtime execution using explicit
   control flow rather than unconditional CLR `finally`.
10. Verify the model with programmatically constructed IR before source syntax
    begins.
11. Add parser nodes for optional `catch` and `finally` clauses.
12. Add binder context and diagnostics for prohibited returns and external loop
    transfers.
13. Lower source-level normal exits, returns, loop transfers, and catchable
    errors through the established exception-region model.
14. Update language, portable IR, MuIR, API, roadmap, and changelog
    documentation.

A future MuIR model may register the source-declared name of each user function
as optional diagnostic metadata. MuIR version 1 continues to expose only the
portable function ID, and runtime stack frames do not infer source names by
encoding them into that ID.

## Considered Approaches

| Approach | Compatibility | Source-level recovery | Assessment |
|---|---:|---:|---|
| Host-side `ExecutionResult` only | High | No | Useful host API, but insufficient by itself |
| Provider functions returning explicit failure objects | High | Limited to participating functions | Appropriate for selected expected failures |
| Additive `try`, `catch`, and `finally` in MuLang 1.2 | High across language versions | Yes | Selected approach |

## Recommended Roadmap

1. Introduce `RuntimeError`, categories, catchability, and MuLang stack frames.
2. Keep `MuLangRuntimeException` as the host propagation mechanism.
3. Add `ExecutionResult` without immediately removing the existing API.
4. Define an explicit contract for provider application failures.
5. Introduce the built-in `error` type and extend the unreleased MuIR version 2
   with lifetime and exception regions.
6. Add source-level `try` and `catch` on the established region model.
7. Add source-level `throw`, rethrow, and any later filtering facilities.
8. Add `finally` after the base control-flow model has stabilized, using the
   cleanup semantics and MuIR requirements defined above.
