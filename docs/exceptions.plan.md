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

Source-level `try`, `catch`, `throw`, the built-in `error` type, and MuIR
exception regions remain deferred to the MuLang 1.2 work.

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

The current implementation converts every provider exception into
`ProviderFailure`. The new model distinguishes expected application failures
from unexpected implementation failures.

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

The new language version introduces a minimal protected statement:

```text
try {
    ...
} catch (error) {
    ...
}
```

Every `try` has exactly one `catch`. The catch parameter is optional:

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

The built-in `error` type exposes at least:

- `code: string`;
- `category: string`, or an intrinsic category representation;
- `message: string`;
- `cause: error?`;
- `data?: unknown?`;
- source location information.

The properties of `error` are declared read-only through the general read-only
object-property model. Error values do not introduce special mutability rules.
This part therefore depends on the specification and implementation of
read-only object properties.

`cause` is populated at the discretion of the runtime, provider, or MuLang code
that creates the error. It is not derived automatically from the host exception
chain.

`data` is an optional channel for arbitrary application information. The
payload preserves its representation, identity, and mutability. The `data`
property itself is read-only, but an object or array stored in it does not
become deeply immutable.

### Catch Semantics

The initial semantics are:

- `catch` handles only errors explicitly marked as catchable;
- completed side effects are not rolled back;
- an error raised by the handler propagates normally;
- every `try` has exactly one `catch`;
- the catch parameter is optional and read-only when present;
- handlers have no filters or type hierarchy;
- `finally` is not part of the initial feature.

The lack of rollback is intentional. Effects completed by a statement or
provider function before an error remain observable.

### Deferred `finally`

`finally` significantly complicates the semantics and lowering of:

- `return`;
- `break`;
- `continue`;
- errors raised in the protected block;
- errors raised in the handler;
- errors raised while executing `finally`.

It can be added after the initial control-flow model has stabilized.

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
- optional `cause`;
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
- an absent or `null` cause becomes `null`;
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

Introducing `try`, `catch`, `throw`, and eventually `finally` reserves new
identifiers.

Exception handling therefore belongs to a new language version, most likely
MuLang 1.2. Versions 1 and 1.1 continue to tokenize these names according to
their existing rules, preserving source compatibility.

An exception-handling language-profile option may additionally control feature
availability. Such an option remains subordinate to a language version that
defines the syntax.

## MuIR Compatibility

MuIR version 1 cannot represent exception handling. The feature therefore
requires a new MuIR format version. New executable instructions, terminators, or
protected-region structures are incompatible with the current format contract.

A possible representation includes:

- protected regions associated with basic blocks;
- handler blocks;
- a slot receiving the caught error;
- a `throw` instruction or terminator;
- validation rules for entering and leaving protected regions;
- automatic propagation of uncatchable errors.

The representation must remain independent from exception-handling facilities
provided by a target platform so that MuIR remains portable.

A future MuIR model may register the source-declared name of each user function
as optional diagnostic metadata. MuIR version 1 continues to expose only the
portable function ID, and runtime stack frames do not infer source names by
encoding them into that ID.

## Considered Approaches

| Approach | Compatibility | Source-level recovery | Assessment |
|---|---:|---:|---|
| Host-side `ExecutionResult` only | High | No | Useful host API, but insufficient by itself |
| Provider functions returning explicit failure objects | High | Limited to participating functions | Appropriate for selected expected failures |
| Additive `try` and `catch` in MuLang 1.2 | High across language versions | Yes | Selected approach |

## Recommended Roadmap

1. Introduce `RuntimeError`, categories, catchability, and MuLang stack frames.
2. Keep `MuLangRuntimeException` as the host propagation mechanism.
3. Add `ExecutionResult` without immediately removing the existing API.
4. Define an explicit contract for provider application failures.
5. Introduce `try`, `catch`, and the built-in `error` type in MuLang 1.2.
6. Define a new MuIR version with protected regions and error propagation.
7. Add source-level `throw`, rethrow, and any later filtering facilities.
8. Evaluate `finally` only after the base control-flow model has stabilized.
