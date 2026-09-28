# 12. Errors and diagnostics

## 12.1. Compile-time diagnostics

A compile-time diagnostic MUST contain:

- a severity;
- a source range;
- a message.

An identifier that becomes a reserved keyword in a later supported language version produces a warning on every occurrence. Under the selected earlier version, the token remains an identifier.

Syntax or capabilities disabled by the selected language profile produce compile-time errors as defined in [Section 14.10](14-language-profiles.md#1410-feature-diagnostics).

No executable result may be produced when an error diagnostic is present.

When compile-time constant evaluation is enabled, failure while evaluating a required constant expression is a compile-time error as defined in [Section 8.11](08-expressions.md#811-compile-time-constant-evaluation).

## 12.2. Runtime errors

A runtime failure MUST be represented as a MuLang runtime error containing:

- a category;
- a message;
- the source range of the operation;
- an optional underlying host failure.

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

A failure from a host-provided function MUST be represented as a MuLang runtime error. The underlying failure SHOULD remain available to the host when the execution environment can preserve it.

The language provides no source-level mechanism for catching runtime errors.

## 12.3. Intentionally non-preventable runtime errors

Property assignment, array element assignment, or property removal can be rejected by a host-provided value even though no source-level capability predicate is available.

A semantic checked-cast failure is preventable by testing the same unchanged value with the corresponding `is` expression. Operational failures encountered while evaluating either operation remain possible.

Host failures, environment incompatibility, cancellation, budget exhaustion, and call-depth exhaustion are controlled outside the source program and are not semantic check gaps.
