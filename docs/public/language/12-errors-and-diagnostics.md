# 12. Errors and diagnostics

## 12.1. Compile-time diagnostics

A compile-time diagnostic MUST contain:

- a severity;
- a source range;
- a message.

An identifier that becomes a reserved keyword in a later supported language version produces a warning on every occurrence. Under the selected earlier version, the token remains an identifier.

Syntax or capabilities disabled by the selected language profile produce compile-time errors as defined in [Section 14.11](14-language-profiles.md#1411-feature-diagnostics).

No executable result may be produced when an error diagnostic is present.

When compile-time constant evaluation is enabled, failure while evaluating a required constant expression is a compile-time error as defined in [Section 8.11](08-expressions.md#811-compile-time-constant-evaluation).

> For example, each of these sources produces a compile-time error:
>
> ```text
> var value: int = "text";
> ```
>
> ```text
> 1 / 0
> ```
>
> The first is a type mismatch. Under the standard profile, the second fails during constant evaluation.

## 12.2. Runtime errors

A runtime failure MUST be represented as a MuLang runtime error containing:

- a stable code;
- a category;
- explicit catchability;
- a message;
- the source range of the operation;
- MuLang stack frames ordered from innermost to outermost;
- an optional public MuLang error cause;
- optional application data;
- an optional underlying host failure.

The public cause is selected explicitly by the error creator. It is not an
automatic projection of an underlying host exception.

Runtime-error categories are:

| Category | Meaning |
|---|---|
| `Operation` | An intrinsic operation failed |
| `Mutation` | An object or array mutation or removal was rejected |
| `Application` | MuLang code or a provider intentionally reported an application failure |
| `Provider` | A provider implementation failed unexpectedly |
| `Resource` | An execution resource limit was exhausted |
| `Cancellation` | Execution was cancelled |
| `Environment` | The runtime environment was incompatible or incomplete |
| `RuntimeContract` | A runtime value violated a declared contract |

Catchability MUST be represented independently from the category and code.

Each MuLang stack frame contains a portable function identifier and the call
span that entered the function.

Runtime errors include:

- null operand where a non-null value is required;
- failed checked cast;
- integer overflow;
- division or remainder by integer zero;
- invalid shift count;
- invalid array index;
- absent property access;
- prohibited property or array mutation;
- prohibited property removal;
- incompatible execution environment;
- exhausted execution budget;
- cancellation;
- failure of a host-provided function;
- exhausted user-function call depth.

When compile-time constant evaluation is enabled and an applicable operation is evaluated as a required constant expression, the corresponding failure is reported during compilation instead.

A declared application failure from a host-provided function MUST use category
`Application` and be catchable. An unexpected provider implementation failure
MUST use category `Provider` and be uncatchable. The underlying host failure
SHOULD remain available to the host when the execution environment can preserve
it.

The language provides no source-level mechanism for catching runtime errors.

> Given `value: unknown`, this expression compiles but produces a failed-cast runtime error when the runtime value is not an `int`:
>
> ```text
> value as int
> ```
>
> Given `values: int[]`, this expression produces an invalid-index runtime error when the array is empty:
>
> ```text
> values[0]
> ```

## 12.3. Intentionally non-preventable runtime errors

Property assignment, array element assignment, or property removal can be rejected by a host-provided value even though no source-level capability predicate is available.

A semantic checked-cast failure is preventable by testing the same unchanged value with the corresponding `is` expression. Operational failures encountered while evaluating either operation remain possible.

Host failures, environment incompatibility, cancellation, budget exhaustion, and call-depth exhaustion are controlled outside the source program and are not semantic check gaps.

> For example, this source may compile successfully even if the host later rejects the mutation:
>
> ```text
> item.value = 1;
> ```
>
> The static type establishes that assignment is meaningful; the host representation retains the right to reject it at runtime.

## 12.4. Host execution results

An execution API MAY propagate a MuLang runtime error through a host exception
or return a structured execution result.

A structured execution result distinguishes successful completion from failure.
It contains either the execution value or the same structured runtime error that
the exception-based API would expose. A successful null result remains
distinguishable from failure. An underlying host failure remains separately
available to the host when one was preserved by the exception-based API.
